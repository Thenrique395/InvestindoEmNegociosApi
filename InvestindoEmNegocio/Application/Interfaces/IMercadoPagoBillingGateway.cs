using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

/// <summary>
/// Gateway específico do Mercado Pago — vocabulário próprio (preapproval, not "subscription")
/// fica isolado aqui; só sai traduzido (<see cref="ProviderSubscriptionSnapshot"/>) para o
/// resto da aplicação, via <see cref="IPaymentProvider"/>.
/// </summary>
public interface IMercadoPagoBillingGateway
{
    Task<MercadoPagoPreapproval> CreatePreapprovalAsync(
        string reason,
        string payerEmail,
        string externalReference,
        decimal amount,
        string currency,
        SubscriptionBillingCycle billingCycle,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPreapproval> GetPreapprovalAsync(string preapprovalId, CancellationToken cancellationToken = default);

    Task CancelAsync(string preapprovalId, CancellationToken cancellationToken = default);

    Task<MercadoPagoPayment> GetPaymentAsync(string paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reembolso total do pagamento (POST /v1/payments/{id}/refunds, sem corpo). Não cobre
    /// reembolso parcial — não validado contra sandbox real ainda.
    /// </summary>
    Task RefundPaymentAsync(string paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida a assinatura HMAC do webhook (header x-signature) contra o manifest documentado
    /// pelo Mercado Pago: id:{data.id};request-id:{x-request-id};ts:{ts};
    /// </summary>
    bool ValidateWebhookSignature(string dataId, string? requestId, string? signatureHeader);
}

public sealed record MercadoPagoPreapproval(
    string Id,
    string? Status,
    string? ExternalReference,
    string? PayerEmail,
    string? InitPoint,
    decimal? TransactionAmount,
    string? CurrencyId,
    DateTime? NextPaymentDate);

public sealed record MercadoPagoPayment(
    string Id,
    string? Status,
    string? ExternalReference,
    decimal? TransactionAmount);
