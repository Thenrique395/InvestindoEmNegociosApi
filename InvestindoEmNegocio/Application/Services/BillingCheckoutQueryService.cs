using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingCheckoutQueryService(
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IStripeBillingGateway stripeBillingGateway,
    IBillingSubscriptionSyncService billingSubscriptionSyncService) : IBillingCheckoutQueryService
{
    public async Task<BillingCheckoutStatusResponse?> GetCheckoutStatusAsync(Guid userId, Guid checkoutId, CancellationToken cancellationToken = default)
    {
        var checkout = await billingCheckoutRepository.GetByIdAsync(checkoutId, cancellationToken);
        if (checkout is null || checkout.UserId != userId)
            return null;

        return await BuildStatusResponseAsync(userId, checkout, cancellationToken);
    }

    public async Task<BillingCheckoutStatusResponse?> GetCheckoutStatusByProviderSessionAsync(Guid userId, string providerSessionId, CancellationToken cancellationToken = default)
    {
        var checkout = await billingCheckoutRepository.GetByProviderCheckoutIdAsync(providerSessionId, cancellationToken);
        if (checkout is null || checkout.UserId != userId)
            return null;

        if (!string.IsNullOrWhiteSpace(checkout.ProviderCheckoutId))
        {
            var session = await stripeBillingGateway.GetCheckoutSessionAsync(checkout.ProviderCheckoutId, cancellationToken);
            billingSubscriptionSyncService.ApplySessionStatus(checkout, session, DateTime.UtcNow);
            await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
        }

        return await BuildStatusResponseAsync(userId, checkout, cancellationToken);
    }

    private async Task<BillingCheckoutStatusResponse> BuildStatusResponseAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken)
    {
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        return new BillingCheckoutStatusResponse(
            checkout.Id,
            checkout.Provider,
            checkout.Status.ToString(),
            checkout.PlanCode,
            checkout.BillingCycle.ToString(),
            checkout.Amount,
            checkout.Currency,
            checkout.ProviderCheckoutId,
            checkout.ProviderSubscriptionId,
            checkout.ProviderPaymentStatus,
            false,
            checkout.Status is BillingCheckoutStatus.Failed or BillingCheckoutStatus.Expired or BillingCheckoutStatus.Cancelled,
            !string.IsNullOrWhiteSpace(subscription?.ExternalCustomerId),
            subscription?.Status == UserSubscriptionStatus.Active,
            subscription?.AutoRenew ?? false,
            checkout.ExpiresAt,
            checkout.CompletedAt,
            subscription?.RenewsAt,
            subscription?.CancelledAt,
            checkout.RefundedAt,
            checkout.FailureReason);
    }
}
