namespace InvestindoEmNegocio.Application.Interfaces;

public interface IMercadoPagoBillingWebhookService
{
    Task ProcessWebhookAsync(string payload, string? requestId, string? signatureHeader, CancellationToken cancellationToken = default);
}
