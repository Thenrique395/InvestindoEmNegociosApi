using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Services;

public sealed class StripeBillingGateway(IOptions<StripeOptions> stripeOptions) : IStripeBillingGateway
{
    private readonly StripeOptions _options = stripeOptions.Value;

    public async Task<Session> CreateCheckoutSessionAsync(
        User user,
        BillingCheckout checkout,
        string planName,
        string planDescription,
        string? existingCustomerId,
        CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();

        StripeConfiguration.ApiKey = _options.SecretKey;
        var frontendBase = _options.FrontendBaseUrl.TrimEnd('/');
        var successUrl = $"{frontendBase}{_options.SuccessPath}?session_id={{CHECKOUT_SESSION_ID}}&checkout_id={checkout.Id}";
        var cancelUrl = $"{frontendBase}{_options.CancelPath}?checkout_id={checkout.Id}";
        var priceId = ResolveConfiguredPriceId(checkout.PlanCode, checkout.BillingCycle);

        var createOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = user.Id.ToString(),
            PaymentMethodTypes = (_options.PaymentMethodTypes?.Length ?? 0) > 0
                ? (_options.PaymentMethodTypes ?? ["card"]).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                : ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString(),
                ["checkoutId"] = checkout.Id.ToString(),
                ["planCode"] = checkout.PlanCode,
                ["billingCycle"] = checkout.BillingCycle.ToString()
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString(),
                    ["checkoutId"] = checkout.Id.ToString(),
                    ["planCode"] = checkout.PlanCode,
                    ["billingCycle"] = checkout.BillingCycle.ToString()
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(existingCustomerId))
            createOptions.Customer = existingCustomerId;
        else
            createOptions.CustomerEmail = user.Email;

        createOptions.LineItems =
        [
            !string.IsNullOrWhiteSpace(priceId)
                ? new SessionLineItemOptions
                {
                    Quantity = 1,
                    Price = priceId
                }
                : new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "brl",
                        UnitAmount = ToCents(checkout.Amount),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = checkout.BillingCycle == SubscriptionBillingCycle.Yearly ? "year" : "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Investindo em Negócios - {planName}",
                            Description = planDescription
                        }
                    }
                }
        ];

        var sessionService = new SessionService();
        return await sessionService.CreateAsync(createOptions, cancellationToken: cancellationToken);
    }

    public async Task<Session> GetCheckoutSessionAsync(string providerSessionId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var sessionService = new SessionService();
        return await sessionService.GetAsync(providerSessionId, cancellationToken: cancellationToken);
    }

    public async Task<string> CreatePortalSessionAsync(string externalCustomerId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var sessionService = new Stripe.BillingPortal.SessionService();
        var frontendBase = _options.FrontendBaseUrl.TrimEnd('/');
        var session = await sessionService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = externalCustomerId,
            ReturnUrl = $"{frontendBase}{_options.PortalReturnPath}"
        }, cancellationToken: cancellationToken);

        return session.Url;
    }

    public Event ConstructWebhookEvent(string payload, string signatureHeader)
    {
        EnsureWebhookConfigured();
        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new AppProblemException("Webhook inválido", "Assinatura do webhook ausente.", StatusCodes.Status400BadRequest);

        StripeConfiguration.ApiKey = _options.SecretKey;
        return EventUtility.ConstructEvent(payload, signatureHeader, _options.WebhookSecret, throwOnApiVersionMismatch: false);
    }

    public async Task<Subscription> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var service = new SubscriptionService();
        return await service.GetAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task ScheduleCancellationAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var stripeSubscriptionService = new SubscriptionService();
        await stripeSubscriptionService.UpdateAsync(
            subscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
            cancellationToken: cancellationToken);
    }

    public async Task CancelImmediatelyAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var subService = new SubscriptionService();
        await subService.CancelAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task RefundPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var refundService = new RefundService();
        await refundService.CreateAsync(
            new RefundCreateOptions { PaymentIntent = paymentIntentId },
            cancellationToken: cancellationToken);
    }

    public async Task<Invoice?> GetLatestOpenInvoiceAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var service = new InvoiceService();
        var invoices = await service.ListAsync(
            new InvoiceListOptions { Subscription = subscriptionId, Status = "open", Limit = 1 },
            cancellationToken: cancellationToken);
        return invoices.Data.FirstOrDefault();
    }

    public async Task PayInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        EnsureSecretConfigured();
        StripeConfiguration.ApiKey = _options.SecretKey;
        var service = new InvoiceService();
        await service.PayAsync(invoiceId, cancellationToken: cancellationToken);
    }

    private void EnsureSecretConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new AppProblemException("Cobrança indisponível", "Stripe não está configurado neste ambiente.", StatusCodes.Status503ServiceUnavailable);
    }

    private void EnsureWebhookConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(_options.WebhookSecret))
            throw new AppProblemException("Cobrança indisponível", "Stripe não está configurado neste ambiente.", StatusCodes.Status503ServiceUnavailable);
    }

    private string? ResolveConfiguredPriceId(string planCode, SubscriptionBillingCycle cycle)
    {
        if (_options.PriceIds is null || _options.PriceIds.Count == 0)
            return null;

        var key = $"{planCode}.{cycle}".ToLowerInvariant();
        return _options.PriceIds.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static long ToCents(decimal amount)
        => Convert.ToInt64(Math.Round(amount * 100m, MidpointRounding.AwayFromZero));
}
