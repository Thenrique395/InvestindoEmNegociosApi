using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Chamadas REST diretas à API do Mercado Pago — não existe um SDK .NET oficial tão maduro
/// quanto o do Stripe, então HTTP direto é mais previsível e mantém o vocabulário do MP
/// (preapproval, auto_recurring) isolado nesta classe.
///
/// Mapeamento de status preapproval → vocabulário canônico ainda não validado contra o
/// sandbox real do MP (sem credenciais disponíveis nesta fase) — revisar ao integrar de
/// ponta a ponta.
/// </summary>
public sealed class MercadoPagoBillingGateway(HttpClient httpClient, IOptions<MercadoPagoOptions> options)
    : IMercadoPagoBillingGateway, IPaymentProvider
{
    private readonly MercadoPagoOptions _options = options.Value;

    public async Task<MercadoPagoPreapproval> CreatePreapprovalAsync(
        string reason,
        string payerEmail,
        string externalReference,
        decimal amount,
        string currency,
        SubscriptionBillingCycle billingCycle,
        CancellationToken cancellationToken = default)
    {
        EnsureAccessTokenConfigured();

        var frontendBase = _options.FrontendBaseUrl.TrimEnd('/');
        var request = new CreatePreapprovalRequest
        {
            Reason = reason,
            ExternalReference = externalReference,
            PayerEmail = payerEmail,
            BackUrl = $"{frontendBase}{_options.SuccessPath}?external_reference={externalReference}",
            Status = "pending",
            AutoRecurring = new AutoRecurringRequest
            {
                Frequency = billingCycle == SubscriptionBillingCycle.Yearly ? 12 : 1,
                FrequencyType = "months",
                TransactionAmount = amount,
                CurrencyId = currency.ToUpperInvariant()
            }
        };

        using var httpRequest = BuildRequest(HttpMethod.Post, "/preapproval", request);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<PreapprovalResponse>(cancellationToken: cancellationToken)
            ?? throw new AppProblemException("Cobrança indisponível", "Resposta vazia do Mercado Pago.", StatusCodes.Status502BadGateway);
        return body.ToPreapproval();
    }

    public async Task<MercadoPagoPreapproval> GetPreapprovalAsync(string preapprovalId, CancellationToken cancellationToken = default)
    {
        EnsureAccessTokenConfigured();

        using var httpRequest = BuildRequest(HttpMethod.Get, $"/preapproval/{preapprovalId}");
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<PreapprovalResponse>(cancellationToken: cancellationToken)
            ?? throw new AppProblemException("Cobrança indisponível", "Resposta vazia do Mercado Pago.", StatusCodes.Status502BadGateway);
        return body.ToPreapproval();
    }

    public async Task CancelAsync(string preapprovalId, CancellationToken cancellationToken = default)
    {
        EnsureAccessTokenConfigured();

        using var httpRequest = BuildRequest(HttpMethod.Put, $"/preapproval/{preapprovalId}", new UpdatePreapprovalStatusRequest("cancelled"));
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<MercadoPagoPayment> GetPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        EnsureAccessTokenConfigured();

        using var httpRequest = BuildRequest(HttpMethod.Get, $"/v1/payments/{paymentId}");
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: cancellationToken)
            ?? throw new AppProblemException("Cobrança indisponível", "Resposta vazia do Mercado Pago.", StatusCodes.Status502BadGateway);
        return body.ToPayment();
    }

    public async Task RefundPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        EnsureAccessTokenConfigured();

        using var httpRequest = BuildRequest(HttpMethod.Post, $"/v1/payments/{paymentId}/refunds", new { });
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public bool ValidateWebhookSignature(string dataId, string? requestId, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var parts = signatureHeader.Split(',')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var receivedHash))
            return false;

        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(receivedHash));
    }

    async Task<ProviderSubscriptionSnapshot> IPaymentProvider.GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken) =>
        (await GetPreapprovalAsync(subscriptionId, cancellationToken)).ToProviderSnapshot();

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AppProblemException(
            "Cobrança indisponível",
            $"Mercado Pago retornou {(int)response.StatusCode}: {detail}",
            StatusCodes.Status502BadGateway);
    }

    private void EnsureAccessTokenConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            throw new AppProblemException("Cobrança indisponível", "Mercado Pago não está configurado neste ambiente.", StatusCodes.Status503ServiceUnavailable);
    }

    private sealed class CreatePreapprovalRequest
    {
        [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
        [JsonPropertyName("external_reference")] public string ExternalReference { get; set; } = string.Empty;
        [JsonPropertyName("payer_email")] public string PayerEmail { get; set; } = string.Empty;
        [JsonPropertyName("back_url")] public string BackUrl { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = "pending";
        [JsonPropertyName("auto_recurring")] public AutoRecurringRequest AutoRecurring { get; set; } = new();
    }

    private sealed class AutoRecurringRequest
    {
        [JsonPropertyName("frequency")] public int Frequency { get; set; }
        [JsonPropertyName("frequency_type")] public string FrequencyType { get; set; } = "months";
        [JsonPropertyName("transaction_amount")] public decimal TransactionAmount { get; set; }
        [JsonPropertyName("currency_id")] public string CurrencyId { get; set; } = "BRL";
    }

    private sealed record UpdatePreapprovalStatusRequest([property: JsonPropertyName("status")] string Status);

    private sealed class PreapprovalResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("external_reference")] public string? ExternalReference { get; set; }
        [JsonPropertyName("payer_email")] public string? PayerEmail { get; set; }
        [JsonPropertyName("init_point")] public string? InitPoint { get; set; }
        [JsonPropertyName("next_payment_date")] public DateTime? NextPaymentDate { get; set; }
        [JsonPropertyName("auto_recurring")] public AutoRecurringResponse? AutoRecurring { get; set; }

        public MercadoPagoPreapproval ToPreapproval() => new(
            Id,
            Status,
            ExternalReference,
            PayerEmail,
            InitPoint,
            AutoRecurring?.TransactionAmount,
            AutoRecurring?.CurrencyId,
            NextPaymentDate);
    }

    private sealed class AutoRecurringResponse
    {
        [JsonPropertyName("transaction_amount")] public decimal? TransactionAmount { get; set; }
        [JsonPropertyName("currency_id")] public string? CurrencyId { get; set; }
    }

    private sealed class PaymentResponse
    {
        // O id de pagamento do MP é numérico na API real; convertido para string para manter
        // consistência com o resto do código (que trata todo id de gateway como string).
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("external_reference")] public string? ExternalReference { get; set; }
        [JsonPropertyName("transaction_amount")] public decimal? TransactionAmount { get; set; }

        public MercadoPagoPayment ToPayment() => new(Id.ToString(), Status, ExternalReference, TransactionAmount);
    }
}
