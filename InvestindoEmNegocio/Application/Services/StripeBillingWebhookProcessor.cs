using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Application.Services;

public sealed class StripeBillingWebhookProcessor(
    IBillingCheckoutRepository billingCheckoutRepository,
    IBillingSubscriptionSyncService billingSubscriptionSyncService,
    ILogger<StripeBillingWebhookProcessor> logger) : IStripeBillingWebhookProcessor
{
    public async Task ProcessAsync(Event stripeEvent, BillingWebhookEvent webhookLog, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing Stripe webhook {EventId} type={EventType}", stripeEvent.Id, stripeEvent.Type);

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
                if (checkout is null)
                {
                    logger.LogWarning("Stripe webhook {EventId}: session {SessionId} not found — ignoring", stripeEvent.Id, session.Id);
                    return;
                }

                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                logger.LogInformation("Stripe webhook {EventId}: applying session status for checkout {CheckoutId} (user {UserId})", stripeEvent.Id, checkout.Id, checkout.UserId);
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
                logger.LogInformation("Stripe webhook {EventId}: syncing subscription {SubscriptionId} (type={EventType})", stripeEvent.Id, subscription.Id, stripeEvent.Type);
                await billingSubscriptionSyncService.SyncAsync(subscription, stripeEvent.Type, cancellationToken);
                break;
            }
            case EventTypes.InvoicePaid:
            case EventTypes.InvoicePaymentFailed:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                var subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
                if (subscriptionId is null)
                {
                    logger.LogWarning("Stripe webhook {EventId}: invoice event without subscriptionId — ignoring", stripeEvent.Id);
                    return;
                }
                var resolvedInvoice = invoice!;
                var checkout = await billingCheckoutRepository.GetByProviderSubscriptionIdAsync(subscriptionId, cancellationToken);
                if (checkout is not null)
                {
                    webhookLog.AttachContext(checkout.UserId, checkout.Id);
                    if (stripeEvent.Type == EventTypes.InvoicePaid)
                    {
                        logger.LogInformation("Stripe webhook {EventId}: invoice paid for subscription {SubscriptionId}, checkout {CheckoutId}", stripeEvent.Id, subscriptionId, checkout.Id);
                        checkout.MarkPaid(resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);
                    }
                    else
                    {
                        logger.LogWarning("Stripe webhook {EventId}: invoice payment failed for subscription {SubscriptionId}, checkout {CheckoutId}", stripeEvent.Id, subscriptionId, checkout.Id);
                        checkout.MarkFailed(resolvedInvoice.LastFinalizationError?.Message ?? "Pagamento não aprovado.", resolvedInvoice.Status, stripeEvent.Type, DateTime.UtcNow);
                    }

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
                if (checkout is null)
                {
                    logger.LogWarning("Stripe webhook {EventId}: charge refund for PaymentIntent {PaymentIntentId} — checkout not found", stripeEvent.Id, charge.PaymentIntentId);
                    return;
                }
                webhookLog.AttachContext(checkout.UserId, checkout.Id);
                logger.LogInformation("Stripe webhook {EventId}: charge refunded for checkout {CheckoutId} (user {UserId})", stripeEvent.Id, checkout.Id, checkout.UserId);
                checkout.MarkRefunded(stripeEvent.Type, DateTime.UtcNow);
                await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
                await billingSubscriptionSyncService.DowngradeUserAfterRefundAsync(checkout, cancellationToken);
                break;
            }
            default:
                logger.LogDebug("Stripe webhook {EventId} type={EventType}: no handler — skipped", stripeEvent.Id, stripeEvent.Type);
                break;
        }
    }
}
