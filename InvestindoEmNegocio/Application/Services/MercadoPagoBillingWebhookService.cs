using System.Text.Json;
using System.Text.Json.Serialization;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Processa notificações de webhook do Mercado Pago. Formato do payload (action/type/data.id)
/// e nomes de header (x-signature/x-request-id) seguem a documentação pública do MP, mas ainda
/// não foram validados contra um envio real de sandbox — revisar ao integrar de ponta a ponta.
/// </summary>
public sealed class MercadoPagoBillingWebhookService(
    IBillingWebhookEventRepository billingWebhookEventRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IMercadoPagoBillingGateway mercadoPagoBillingGateway,
    IBillingSubscriptionSyncService billingSubscriptionSyncService,
    ILogger<MercadoPagoBillingWebhookService> logger) : IMercadoPagoBillingWebhookService
{
    private const string ProviderName = "mercado_pago";
    private const string PreapprovalNotificationType = "subscription_preapproval";
    private const string PaymentNotificationType = "payment";

    public async Task ProcessWebhookAsync(string payload, string? requestId, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        MercadoPagoWebhookNotification notification;
        try
        {
            notification = JsonSerializer.Deserialize<MercadoPagoWebhookNotification>(payload)
                ?? throw new AppProblemException("Webhook inválido", "Payload vazio.", StatusCodes.Status400BadRequest);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Mercado Pago webhook: payload inválido.");
            throw new AppProblemException("Webhook inválido", "Payload não é um JSON válido.", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(notification.Data?.Id))
        {
            logger.LogDebug("Mercado Pago webhook type={Type}: sem data.id — ignorado", notification.Type);
            return;
        }

        if (!mercadoPagoBillingGateway.ValidateWebhookSignature(notification.Data.Id, requestId, signatureHeader))
        {
            logger.LogWarning("Mercado Pago webhook: assinatura inválida para data.id={DataId}", notification.Data.Id);
            throw new AppProblemException("Webhook inválido", "Assinatura do webhook inválida.", StatusCodes.Status400BadRequest);
        }

        var providerEventId = notification.NotificationId?.ToString() ?? $"{notification.Type}:{notification.Data.Id}:{notification.DateCreated:O}";
        var existing = await billingWebhookEventRepository.GetByProviderEventIdAsync(ProviderName, providerEventId, cancellationToken);
        if (existing is not null)
            return;

        var webhookLog = new BillingWebhookEvent(providerEventId, notification.Type ?? "unknown", payload, provider: ProviderName);
        await billingWebhookEventRepository.AddAsync(webhookLog, cancellationToken);
        await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);

        try
        {
            if (notification.Type == PreapprovalNotificationType)
            {
                var preapproval = await mercadoPagoBillingGateway.GetPreapprovalAsync(notification.Data.Id, cancellationToken);
                await billingSubscriptionSyncService.SyncAsync(preapproval.ToProviderSnapshot(), $"mercadopago.{notification.Type}.{notification.Action}", cancellationToken);
            }
            else if (notification.Type == PaymentNotificationType)
            {
                await LinkPaymentToCheckoutAsync(notification.Data.Id, cancellationToken);
            }
            else
            {
                logger.LogDebug("Mercado Pago webhook type={Type}: sem handler — ignorado", notification.Type);
            }

            webhookLog.MarkProcessed(true, null, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            webhookLog.MarkProcessed(false, ex.Message, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Failed to process Mercado Pago webhook {Type}", notification.Type);
            throw;
        }
    }

    /// <summary>
    /// Busca o pagamento no MP e, se o external_reference corresponder a um checkout local,
    /// grava o id do pagamento em ProviderPaymentIntentId — é o que permite reembolsar
    /// depois (a API de refund do MP exige o id do pagamento, não o do preapproval).
    /// Pressupõe que o MP propaga o external_reference do preapproval para o pagamento gerado
    /// por ele — não validado contra sandbox real ainda.
    /// </summary>
    private async Task LinkPaymentToCheckoutAsync(string paymentId, CancellationToken cancellationToken)
    {
        var payment = await mercadoPagoBillingGateway.GetPaymentAsync(paymentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(payment.ExternalReference) || !Guid.TryParse(payment.ExternalReference, out var checkoutId))
        {
            logger.LogDebug("Mercado Pago webhook payment {PaymentId}: sem external_reference reconhecível — não vinculado a nenhum checkout.", paymentId);
            return;
        }

        var checkout = await billingCheckoutRepository.GetByIdAsync(checkoutId, cancellationToken);
        if (checkout is null)
        {
            logger.LogWarning("Mercado Pago webhook payment {PaymentId}: checkout {CheckoutId} não encontrado.", paymentId, checkoutId);
            return;
        }

        checkout.AttachProviderObjects(null, null, payment.Id, DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    private sealed class MercadoPagoWebhookNotification
    {
        [JsonPropertyName("id")] public long? NotificationId { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("date_created")] public DateTime? DateCreated { get; set; }
        [JsonPropertyName("data")] public MercadoPagoWebhookData? Data { get; set; }
    }

    private sealed class MercadoPagoWebhookData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }
}
