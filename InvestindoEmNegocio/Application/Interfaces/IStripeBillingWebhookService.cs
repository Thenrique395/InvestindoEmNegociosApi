namespace InvestindoEmNegocio.Application.Interfaces;

public interface IStripeBillingWebhookService
{
    Task ProcessStripeWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default);
}
