using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Services;

public sealed class StripeBillingWebhookProcessor(
    IBillingCheckoutRepository billingCheckoutRepository,
    IBillingSubscriptionSyncService billingSubscriptionSyncService) : IStripeBillingWebhookProcessor
{
    public async Task ProcessAsync(Event stripeEvent, BillingWebhookEvent webhookLog, CancellationToken cancellationToken = default)
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
                var checkout = await billingSubscriptionSyncService.ResolveCheckoutFromSessionAsync(session, cancellationToken);
                if (checkout is null) return;

                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                billingSubscriptionSyncService.ApplySessionStatus(checkout, session, DateTime.UtcNow, stripeEvent.Type);
                await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

                if (session.SubscriptionId is not null)
                    await billingSubscriptionSyncService.SyncByExternalSubscriptionAsync(session.SubscriptionId, checkout, cancellationToken);

                break;
            }
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription is null) return;
                await billingSubscriptionSyncService.SyncAsync(subscription, stripeEvent.Type, cancellationToken);
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
                        checkout.MarkPaid(resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);
                    else
                        checkout.MarkFailed(resolvedInvoice.LastFinalizationError?.Message ?? "Pagamento não aprovado.", resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);

                    await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                }

                await billingSubscriptionSyncService.SyncByExternalSubscriptionAsync(subscriptionId, checkout, cancellationToken);
                break;
            }
            case EventTypes.ChargeRefunded:
            {
                var charge = stripeEvent.Data.Object as Charge;
                if (charge?.PaymentIntentId is null) return;
                var checkout = await billingSubscriptionSyncService.FindCheckoutByPaymentIntentAsync(charge.PaymentIntentId, cancellationToken);
                if (checkout is null) return;
                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                checkout.MarkRefunded(stripeEvent.Type, DateTime.UtcNow);
                await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                await billingSubscriptionSyncService.DowngradeUserAfterRefundAsync(checkout, cancellationToken);
                break;
            }
        }
    }
}
