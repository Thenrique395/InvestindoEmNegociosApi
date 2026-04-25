using InvestindoEmNegocio.Domain.Entities;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBillingSubscriptionSyncService
{
    Task SyncByExternalSubscriptionAsync(string subscriptionId, BillingCheckout? checkout, CancellationToken cancellationToken = default);
    Task SyncAsync(Subscription subscription, string eventType, CancellationToken cancellationToken = default, BillingCheckout? knownCheckout = null);
    void ApplySessionStatus(BillingCheckout checkout, Session session, DateTime nowUtc, string eventType = "provider.sync");
    Task<BillingCheckout?> ResolveCheckoutFromSessionAsync(Session session, CancellationToken cancellationToken = default);
    Task<BillingCheckout?> FindCheckoutByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);
    Task DowngradeUserAfterRefundAsync(BillingCheckout checkout, CancellationToken cancellationToken = default);
}
