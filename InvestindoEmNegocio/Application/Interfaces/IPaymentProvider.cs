namespace InvestindoEmNegocio.Application.Interfaces;

/// <summary>
/// Estado de uma assinatura no gateway de pagamento, em vocabulário genérico — usado para
/// reconciliação (webhook e robô) sem vazar o tipo do SDK de um gateway específico (Stripe,
/// Mercado Pago, etc.) para a camada de Application.
/// </summary>
public sealed record ProviderSubscriptionSnapshot(
    string ExternalId,
    string? CustomerId,
    string? Status,
    string? PriceId,
    DateTime? CurrentPeriodEnd,
    bool? CancelAtPeriodEnd,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary>
/// Abstração de gateway de pagamento, agnóstica de provedor. Hoje só expõe o que a
/// reconciliação de assinatura (webhook + robô de expiração) precisa; outras operações
/// (checkout, portal, reembolso, fatura) seguem específicas de cada gateway
/// (<see cref="IStripeBillingGateway"/>) até existir um segundo provedor real implementado.
/// </summary>
public interface IPaymentProvider
{
    Task<ProviderSubscriptionSnapshot> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
