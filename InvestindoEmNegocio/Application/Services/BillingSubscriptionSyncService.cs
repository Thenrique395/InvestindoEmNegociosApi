using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingSubscriptionSyncService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IStripeBillingGateway stripeBillingGateway,
    IBillingNotificationService billingNotificationService,
    ILogger<BillingSubscriptionSyncService> logger) : IBillingSubscriptionSyncService
{
    public async Task SyncByExternalSubscriptionAsync(string subscriptionId, BillingCheckout? checkout, CancellationToken cancellationToken = default)
    {
        var subscription = await stripeBillingGateway.GetSubscriptionAsync(subscriptionId, cancellationToken);
        await SyncAsync(subscription, "provider.sync", cancellationToken, checkout);
    }

    public async Task SyncAsync(Subscription subscription, string eventType, CancellationToken cancellationToken = default, BillingCheckout? knownCheckout = null)
    {
        var checkout = knownCheckout ?? await billingCheckoutRepository.GetByProviderSubscriptionIdAsync(subscription.Id, cancellationToken);
        if (checkout is null
            && subscription.Metadata is not null
            && subscription.Metadata.TryGetValue("checkoutId", out var checkoutIdRaw)
            && Guid.TryParse(checkoutIdRaw, out var checkoutId))
        {
            checkout = await billingCheckoutRepository.GetByIdAsync(checkoutId, cancellationToken);
        }

        UserSubscription? localSubscription = await userSubscriptionRepository.GetByExternalSubscriptionIdAsync(subscription.Id, cancellationToken);
        if (localSubscription is null && checkout is not null)
            localSubscription = await userSubscriptionRepository.GetByUserIdAsync(checkout.UserId, cancellationToken);

        if (checkout is not null)
            checkout.AttachProviderObjects(subscription.CustomerId, subscription.Id, checkout.ProviderPaymentIntentId, DateTime.UtcNow);

        if (checkout is null && localSubscription is null)
            return;

        if (localSubscription is null)
        {
            var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(checkout!.PlanCode);
            localSubscription = new UserSubscription(checkout.UserId, checkout.PlanCode, plan.Role, checkout.BillingCycle, checkout.Amount, checkout.Currency, DateTime.UtcNow, ResolveRenewalAt(subscription));
            await userSubscriptionRepository.AddAsync(localSubscription, cancellationToken);
        }

        var status = subscription.Status?.ToLowerInvariant() ?? string.Empty;
        var renewsAt = ResolveRenewalAt(subscription);
        var priceId = subscription.Items?.Data?.FirstOrDefault()?.Price?.Id;

        logger.LogInformation("Syncing subscription {SubscriptionId} status={Status} event={EventType} for user {UserId}",
            subscription.Id, status, eventType, checkout?.UserId ?? localSubscription?.UserId);

        switch (status)
        {
            case "active":
            case "trialing":
                var previousStatus = localSubscription.Status;
                localSubscription.Activate(
                    checkout?.PlanCode ?? localSubscription.PlanCode,
                    checkout?.RoleRequested ?? localSubscription.RoleGranted,
                    checkout?.BillingCycle ?? localSubscription.BillingCycle,
                    checkout?.Amount ?? localSubscription.PriceAmount,
                    checkout?.Currency ?? localSubscription.Currency,
                    DateTime.UtcNow,
                    renewsAt,
                    subscription.CustomerId,
                    subscription.Id,
                    priceId);
                await PromoteUserRoleAsync(localSubscription.UserId, localSubscription.RoleGranted, cancellationToken);
                if (checkout is not null)
                {
                    checkout.MarkPaid(status, eventType, DateTime.UtcNow);
                    if (previousStatus == UserSubscriptionStatus.PastDue)
                    {
                        logger.LogInformation("Subscription {SubscriptionId} reactivated for user {UserId} (was PastDue)", subscription.Id, localSubscription.UserId);
                        await billingNotificationService.NotifyReactivatedAsync(localSubscription.UserId, checkout, cancellationToken);
                    }
                    else if (previousStatus == UserSubscriptionStatus.Active)
                    {
                        logger.LogInformation("Subscription {SubscriptionId} renewed for user {UserId}", subscription.Id, localSubscription.UserId);
                        await billingNotificationService.NotifyRenewalApprovedAsync(localSubscription.UserId, checkout, cancellationToken);
                    }
                    else
                    {
                        logger.LogInformation("Subscription {SubscriptionId} activated for user {UserId}, plan {PlanCode}", subscription.Id, localSubscription.UserId, localSubscription.PlanCode);
                        await billingNotificationService.NotifyApprovedAsync(localSubscription.UserId, checkout, cancellationToken);
                    }
                }
                break;

            case "past_due":
            case "unpaid":
                logger.LogWarning("Subscription {SubscriptionId} marked past_due for user {UserId}", subscription.Id, localSubscription.UserId);
                localSubscription.MarkPastDue(DateTime.UtcNow);
                if (checkout is not null)
                {
                    var isRenewalFailure = checkout.Status == BillingCheckoutStatus.Paid;
                    checkout.MarkFailed("Pagamento pendente ou não aprovado.", status, eventType, DateTime.UtcNow);
                    var failureMessage = isRenewalFailure
                        ? "Tivemos um problema ao renovar sua assinatura. Atualize sua forma de pagamento para continuar com todos os recursos premium."
                        : null;
                    await billingNotificationService.NotifyFailedAsync(localSubscription.UserId, checkout, cancellationToken, failureMessage);
                }
                break;

            case "canceled":
                logger.LogInformation("Subscription {SubscriptionId} canceled for user {UserId} — downgrading to Basic", subscription.Id, localSubscription.UserId);
                localSubscription.CancelNow(DateTime.UtcNow);
                if (checkout is not null)
                    checkout.MarkCancelled(eventType, DateTime.UtcNow);
                await PromoteUserRoleAsync(localSubscription.UserId, UserRole.Basic, cancellationToken);
                break;

            case "incomplete":
                localSubscription.MarkPendingActivation(
                    checkout?.PlanCode ?? localSubscription.PlanCode,
                    checkout?.RoleRequested ?? localSubscription.RoleGranted,
                    checkout?.BillingCycle ?? localSubscription.BillingCycle,
                    checkout?.Amount ?? localSubscription.PriceAmount,
                    checkout?.Currency ?? localSubscription.Currency,
                    DateTime.UtcNow,
                    renewsAt,
                    subscription.CustomerId,
                    subscription.Id,
                    priceId);
                if (checkout is not null)
                    checkout.MarkPending(status, eventType, DateTime.UtcNow);
                break;

            case "incomplete_expired":
                localSubscription.MarkExpired(DateTime.UtcNow);
                if (checkout is not null)
                    checkout.MarkExpired(eventType, DateTime.UtcNow);
                await PromoteUserRoleAsync(localSubscription.UserId, UserRole.Basic, cancellationToken);
                break;
        }

        if (subscription.CancelAtPeriodEnd == true && localSubscription.Status == UserSubscriptionStatus.Active)
            localSubscription.ScheduleCancellation(DateTime.UtcNow);

        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        if (checkout is not null)
            await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public void ApplySessionStatus(BillingCheckout checkout, Session session, DateTime nowUtc, string eventType = "provider.sync")
    {
        checkout.AttachProviderObjects(session.CustomerId, session.SubscriptionId, session.PaymentIntentId, nowUtc);
        var paymentStatus = session.PaymentStatus?.ToLowerInvariant();
        if (paymentStatus == "paid" || paymentStatus == "no_payment_required")
        {
            checkout.MarkPaid(session.PaymentStatus, eventType, nowUtc);
            return;
        }

        if (session.Status?.Equals("expired", StringComparison.OrdinalIgnoreCase) == true)
        {
            checkout.MarkExpired(eventType, nowUtc);
            return;
        }

        if (paymentStatus == "unpaid")
        {
            checkout.MarkFailed("Pagamento não aprovado.", session.PaymentStatus, eventType, nowUtc);
            return;
        }

        checkout.MarkPending(session.PaymentStatus, eventType, nowUtc);
    }

    public async Task<BillingCheckout?> ResolveCheckoutFromSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        BillingCheckout? checkout = null;
        if (session.Metadata is not null
            && session.Metadata.TryGetValue("checkoutId", out var checkoutIdRaw)
            && Guid.TryParse(checkoutIdRaw, out var checkoutId))
        {
            checkout = await billingCheckoutRepository.GetByIdAsync(checkoutId, cancellationToken);
        }

        if (checkout is null && !string.IsNullOrWhiteSpace(session.Id))
            checkout = await billingCheckoutRepository.GetByProviderCheckoutIdAsync(session.Id, cancellationToken);

        return checkout;
    }

    public async Task<BillingCheckout?> FindCheckoutByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
        => await billingCheckoutRepository.GetByProviderPaymentIntentIdAsync(paymentIntentId, cancellationToken);

    public async Task DowngradeUserAfterRefundAsync(BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Downgrading user {UserId} after refund, checkout {CheckoutId}", checkout.UserId, checkout.Id);
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(checkout.UserId, cancellationToken);
        if (subscription is not null)
        {
            subscription.MarkRefunded(DateTime.UtcNow);
            await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        }

        await PromoteUserRoleAsync(checkout.UserId, UserRole.Basic, cancellationToken);
        await billingNotificationService.NotifyFailedAsync(checkout.UserId, checkout, cancellationToken, "O pagamento foi estornado e o plano voltou para o Essencial.");
    }

    private async Task PromoteUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.Role == UserRole.Admin)
            return;

        user.SetRole(role);
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private static DateTime ResolveRenewalAt(Subscription subscription)
    {
        var currentPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (currentPeriodEnd is DateTime dt && dt > DateTime.MinValue)
            return dt.ToUniversalTime();

        return DateTime.UtcNow.AddMonths(1);
    }
}
