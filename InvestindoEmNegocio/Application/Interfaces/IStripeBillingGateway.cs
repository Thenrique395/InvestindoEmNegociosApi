using InvestindoEmNegocio.Domain.Entities;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IStripeBillingGateway
{
    Task<Session> CreateCheckoutSessionAsync(
        User user,
        BillingCheckout checkout,
        string planName,
        string planDescription,
        string? existingCustomerId,
        CancellationToken cancellationToken = default);

    Task<Session> GetCheckoutSessionAsync(string providerSessionId, CancellationToken cancellationToken = default);
    Task<string> CreatePortalSessionAsync(string externalCustomerId, CancellationToken cancellationToken = default);
    Event ConstructWebhookEvent(string payload, string signatureHeader);
    Task<Subscription> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task ScheduleCancellationAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
