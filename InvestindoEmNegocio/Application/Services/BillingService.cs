using System.Globalization;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IBillingWebhookEventRepository billingWebhookEventRepository,
    IUserNotificationRepository notificationRepository,
    IEmailSender emailSender,
    IOptions<StripeOptions> stripeOptions,
    ILogger<BillingService> logger) : IBillingService
{
    private readonly StripeOptions _options = stripeOptions.Value;

    public async Task<StartBillingCheckoutResponse> StartCheckoutAsync(Guid userId, StartBillingCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(request.PlanCode);
        if (plan.Role == UserRole.Admin)
            throw new AppProblemException("Plano inválido", "Plano Admin não está disponível para contratação self-service.", StatusCodes.Status400BadRequest);

        if (!Enum.TryParse<SubscriptionBillingCycle>(request.BillingCycle, true, out var cycle))
            throw new AppProblemException("Ciclo inválido", "Informe Monthly ou Yearly.", StatusCodes.Status400BadRequest);

        if (plan.Code == "basic")
            throw new AppProblemException("Plano gratuito", "O plano Essencial não requer checkout de pagamento.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var amount = cycle == SubscriptionBillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;
        var checkout = new BillingCheckout(userId, plan.Code, plan.Role, cycle, amount, "BRL");
        await billingCheckoutRepository.AddAsync(checkout, cancellationToken);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

        StripeConfiguration.ApiKey = _options.SecretKey;
        var frontendBase = _options.FrontendBaseUrl.TrimEnd('/');
        var successUrl = $"{frontendBase}{_options.SuccessPath}?session_id={{CHECKOUT_SESSION_ID}}&checkout_id={checkout.Id}";
        var cancelUrl = $"{frontendBase}{_options.CancelPath}?checkout_id={checkout.Id}";

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = user.Email,
            ClientReferenceId = user.Id.ToString(),
            PaymentMethodTypes = (_options.PaymentMethodTypes?.Length ?? 0) > 0
                ? (_options.PaymentMethodTypes ?? ["card"]).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                : ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["checkoutId"] = checkout.Id.ToString(),
                ["planCode"] = plan.Code,
                ["billingCycle"] = cycle.ToString()
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "brl",
                        UnitAmount = ToCents(amount),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = cycle == SubscriptionBillingCycle.Yearly ? "year" : "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Investindo em Negócios - {plan.Name}",
                            Description = plan.Description
                        }
                    }
                }
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId.ToString(),
                    ["checkoutId"] = checkout.Id.ToString(),
                    ["planCode"] = plan.Code,
                    ["billingCycle"] = cycle.ToString()
                }
            }
        }, cancellationToken: cancellationToken);

        checkout.Start(session.Id, session.Url ?? string.Empty, session.ExpiresAt, session.PaymentStatus, now);
        checkout.AttachProviderObjects(session.CustomerId, session.SubscriptionId, session.PaymentIntentId, now);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

        await NotifyPendingAsync(user, checkout, cancellationToken);

        return new StartBillingCheckoutResponse(
            checkout.Id,
            checkout.Provider,
            checkout.Status.ToString(),
            checkout.CheckoutUrl ?? string.Empty,
            checkout.PlanCode,
            checkout.BillingCycle.ToString(),
            checkout.Amount,
            checkout.Currency,
            checkout.ExpiresAt);
    }

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
            StripeConfiguration.ApiKey = _options.SecretKey;
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(checkout.ProviderCheckoutId, cancellationToken: cancellationToken);
            ApplySessionStatus(checkout, session, DateTime.UtcNow);
            await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
        }

        return await BuildStatusResponseAsync(userId, checkout, cancellationToken);
    }

    public async Task<BillingPortalSessionResponse> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Assinatura não encontrada", "Nenhuma assinatura encontrada para este usuário.", StatusCodes.Status404NotFound);

        if (string.IsNullOrWhiteSpace(subscription.ExternalCustomerId))
            throw new AppProblemException("Portal indisponível", "Esta assinatura ainda não possui cliente externo vinculado.", StatusCodes.Status400BadRequest);

        StripeConfiguration.ApiKey = _options.SecretKey;
        var sessionService = new Stripe.BillingPortal.SessionService();
        var frontendBase = _options.FrontendBaseUrl.TrimEnd('/');
        var session = await sessionService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = subscription.ExternalCustomerId,
            ReturnUrl = $"{frontendBase}{_options.PortalReturnPath}"
        }, cancellationToken: cancellationToken);

        return new BillingPortalSessionResponse(session.Url);
    }

    public async Task ProcessStripeWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new AppProblemException("Webhook inválido", "Assinatura do webhook ausente.", StatusCodes.Status400BadRequest);

        StripeConfiguration.ApiKey = _options.SecretKey;
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _options.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new AppProblemException("Webhook inválido", "Assinatura do webhook inválida.", StatusCodes.Status400BadRequest);
        }

        var existing = await billingWebhookEventRepository.GetByProviderEventIdAsync("stripe", stripeEvent.Id, cancellationToken);
        if (existing is not null)
            return;

        var webhookLog = new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type, payload);
        await billingWebhookEventRepository.AddAsync(webhookLog, cancellationToken);
        await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await HandleStripeEventAsync(stripeEvent, webhookLog, cancellationToken);
            webhookLog.MarkProcessed(true, null, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            webhookLog.MarkProcessed(false, ex.Message, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Failed to process Stripe webhook {EventType}", stripeEvent.Type);
            throw;
        }
    }

    private async Task HandleStripeEventAsync(Event stripeEvent, BillingWebhookEvent webhookLog, CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            case "checkout.session.async_payment_succeeded":
            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
            {
                var session = stripeEvent.Data.Object as Session;
                if (session is null) return;
                var checkout = await ResolveCheckoutFromSessionAsync(session, cancellationToken);
                if (checkout is null) return;

                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                ApplySessionStatus(checkout, session, DateTime.UtcNow, stripeEvent.Type);
                await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

                if (session.SubscriptionId is not null)
                {
                    await SyncSubscriptionAsync(session.SubscriptionId, checkout, cancellationToken);
                }

                break;
            }
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription is null) return;
                await SyncSubscriptionAsync(subscription, stripeEvent.Type, cancellationToken);
                break;
            }
            case EventTypes.InvoicePaid:
            case EventTypes.InvoicePaymentFailed:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                var subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
                if (subscriptionId is null) return;
                var resolvedInvoice = invoice!;
                var checkout = await billingCheckoutRepository.GetByProviderSubscriptionIdAsync(subscriptionId, cancellationToken);
                if (checkout is not null)
                {
                    webhookLog.AttachContext(checkout.UserId, checkout.Id);
                    if (stripeEvent.Type == EventTypes.InvoicePaid)
                    {
                        checkout.MarkPaid(resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);
                        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        checkout.MarkFailed(resolvedInvoice.LastFinalizationError?.Message ?? "Pagamento não aprovado.", resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);
                        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                    }
                }

                await SyncSubscriptionAsync(subscriptionId, checkout, cancellationToken);
                break;
            }
            case EventTypes.ChargeRefunded:
            {
                var charge = stripeEvent.Data.Object as Charge;
                if (charge?.PaymentIntentId is null) return;
                var checkout = await FindCheckoutByPaymentIntentAsync(charge.PaymentIntentId, cancellationToken);
                if (checkout is null) return;
                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                checkout.MarkRefunded(stripeEvent.Type, DateTime.UtcNow);
                await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                await DowngradeUserAfterRefundAsync(checkout, cancellationToken);
                break;
            }
        }
    }

    private async Task SyncSubscriptionAsync(string subscriptionId, BillingCheckout? checkout, CancellationToken cancellationToken)
    {
        StripeConfiguration.ApiKey = _options.SecretKey;
        var service = new SubscriptionService();
        var subscription = await service.GetAsync(subscriptionId, cancellationToken: cancellationToken);
        await SyncSubscriptionAsync(subscription, "provider.sync", cancellationToken, checkout);
    }

    private async Task SyncSubscriptionAsync(Subscription subscription, string eventType, CancellationToken cancellationToken, BillingCheckout? knownCheckout = null)
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
        {
            localSubscription = await userSubscriptionRepository.GetByUserIdAsync(checkout.UserId, cancellationToken);
        }

        if (checkout is not null)
        {
            checkout.AttachProviderObjects(subscription.CustomerId, subscription.Id, checkout.ProviderPaymentIntentId, DateTime.UtcNow);
        }

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

        switch (status)
        {
            case "active":
            case "trialing":
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
                    await NotifyApprovedAsync(localSubscription.UserId, checkout, cancellationToken);
                }
                break;

            case "past_due":
            case "unpaid":
                localSubscription.MarkPastDue(DateTime.UtcNow);
                if (checkout is not null)
                {
                    checkout.MarkFailed("Pagamento pendente ou não aprovado.", status, eventType, DateTime.UtcNow);
                    await NotifyFailedAsync(localSubscription.UserId, checkout, cancellationToken);
                }
                break;

            case "canceled":
                localSubscription.CancelNow(DateTime.UtcNow);
                if (checkout is not null)
                {
                    checkout.MarkCancelled(eventType, DateTime.UtcNow);
                }
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
                {
                    checkout.MarkPending(status, eventType, DateTime.UtcNow);
                }
                break;

            case "incomplete_expired":
                localSubscription.MarkExpired(DateTime.UtcNow);
                if (checkout is not null)
                {
                    checkout.MarkExpired(eventType, DateTime.UtcNow);
                }
                await PromoteUserRoleAsync(localSubscription.UserId, UserRole.Basic, cancellationToken);
                break;
        }

        if (subscription.CancelAtPeriodEnd == true && localSubscription.Status == UserSubscriptionStatus.Active)
        {
            localSubscription.ScheduleCancellation(DateTime.UtcNow);
        }

        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        if (checkout is not null)
            await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
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

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
        => await userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(_options.WebhookSecret))
            throw new AppProblemException("Billing indisponível", "Stripe não configurado no ambiente.", StatusCodes.Status503ServiceUnavailable);
    }

    private static long ToCents(decimal amount)
        => Convert.ToInt64(Math.Round(amount * 100m, MidpointRounding.AwayFromZero));

    private static DateTime ResolveRenewalAt(Subscription subscription)
    {
        var currentPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (currentPeriodEnd is DateTime dt && dt > DateTime.MinValue)
            return dt.ToUniversalTime();

        return DateTime.UtcNow.AddMonths(1);
    }

    private void ApplySessionStatus(BillingCheckout checkout, Session session, DateTime nowUtc, string eventType = "provider.sync")
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

    private async Task<BillingCheckout?> ResolveCheckoutFromSessionAsync(Session session, CancellationToken cancellationToken)
    {
        BillingCheckout? checkout = null;
        if (session.Metadata is not null
            && session.Metadata.TryGetValue("checkoutId", out var checkoutIdRaw)
            && Guid.TryParse(checkoutIdRaw, out var checkoutId))
            checkout = await billingCheckoutRepository.GetByIdAsync(checkoutId, cancellationToken);

        if (checkout is null && !string.IsNullOrWhiteSpace(session.Id))
            checkout = await billingCheckoutRepository.GetByProviderCheckoutIdAsync(session.Id, cancellationToken);

        return checkout;
    }

    private async Task<BillingCheckout?> FindCheckoutByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        return await billingCheckoutRepository.GetByProviderPaymentIntentIdAsync(paymentIntentId, cancellationToken);
    }

    private async Task PromoteUserRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.Role == UserRole.Admin)
            return;

        user.SetRole(role);
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task DowngradeUserAfterRefundAsync(BillingCheckout checkout, CancellationToken cancellationToken)
    {
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(checkout.UserId, cancellationToken);
        if (subscription is not null)
        {
            subscription.MarkRefunded(DateTime.UtcNow);
            await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        }

        await PromoteUserRoleAsync(checkout.UserId, UserRole.Basic, cancellationToken);
        await NotifyFailedAsync(checkout.UserId, checkout, cancellationToken, "O pagamento foi estornado e o plano voltou para o Essencial.");
    }

    private async Task NotifyPendingAsync(User user, BillingCheckout checkout, CancellationToken cancellationToken)
    {
        if (checkout.EmailPendingSent)
            return;

        await AddInAppNotificationAsync(user.Id, NotificationKind.BillingPending, checkout, "Cobrança iniciada", $"A cobrança do plano {checkout.PlanCode} foi iniciada e aguarda confirmação.", cancellationToken);
        await TrySendEmailAsync(user.Email, "Sua cobrança foi iniciada", BuildEmailHtml("Cobranca iniciada", $"A contratação do plano {checkout.PlanCode} foi iniciada e aguarda confirmação de pagamento."), cancellationToken);
        checkout.MarkPendingEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyApprovedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken)
    {
        if (checkout.EmailSuccessSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        await AddInAppNotificationAsync(userId, NotificationKind.BillingApproved, checkout, "Plano ativado", $"Seu pagamento foi confirmado e o plano {checkout.PlanCode} está ativo.", cancellationToken);
        await TrySendEmailAsync(user.Email, "Pagamento aprovado", BuildEmailHtml("Pagamento aprovado", $"Seu pagamento foi confirmado e o plano {checkout.PlanCode} agora está ativo."), cancellationToken);
        checkout.MarkSuccessEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyFailedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken, string? customMessage = null)
    {
        if (checkout.EmailFailureSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var message = customMessage ?? checkout.FailureReason ?? "A cobrança não foi aprovada e precisa de nova ação.";
        await AddInAppNotificationAsync(userId, NotificationKind.BillingFailed, checkout, "Falha na cobrança", message, cancellationToken);
        await TrySendEmailAsync(user.Email, "Falha na cobrança", BuildEmailHtml("Falha na cobranca", message), cancellationToken);
        checkout.MarkFailureEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task AddInAppNotificationAsync(Guid userId, NotificationKind kind, BillingCheckout checkout, string title, string message, CancellationToken cancellationToken)
    {
        var referenceKey = $"billing:{checkout.Id}:{kind}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, kind, title, message, referenceKey, payloadJson: $$"""{"checkoutId":"{{checkout.Id}}","planCode":"{{checkout.PlanCode}}","status":"{{checkout.Status}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task TrySendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(to, subject, htmlBody, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send billing email {Subject} to {Recipient}", subject, to);
        }
    }

    private static string BuildEmailHtml(string title, string body)
    {
        return $"""
                <html lang="pt-BR">
                <body style="font-family: Arial, sans-serif; color: #0f172a;">
                  <h2>{title}</h2>
                  <p>{body}</p>
                </body>
                </html>
                """;
    }
}
