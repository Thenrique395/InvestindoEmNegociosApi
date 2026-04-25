using InvestindoEmNegocio.Domain.Entities;
using Stripe;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IStripeBillingWebhookProcessor
{
    Task ProcessAsync(Event stripeEvent, BillingWebhookEvent webhookLog, CancellationToken cancellationToken = default);
}
