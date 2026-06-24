using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

internal static class MercadoPagoSubscriptionMapping
{
    public static ProviderSubscriptionSnapshot ToProviderSnapshot(this MercadoPagoPreapproval preapproval)
    {
        IReadOnlyDictionary<string, string>? metadata = string.IsNullOrWhiteSpace(preapproval.ExternalReference)
            ? null
            : new Dictionary<string, string> { ["checkoutId"] = preapproval.ExternalReference };

        return new ProviderSubscriptionSnapshot(
            preapproval.Id,
            preapproval.PayerEmail,
            ToCanonicalStatus(preapproval.Status),
            null, // MP não tem um "Price ID" separado — o valor já vem embutido no preapproval.
            preapproval.NextPaymentDate?.ToUniversalTime(),
            null, // MP não tem "cancelar no fim do período" nativo no preapproval — ver Fase 3.
            metadata);
    }

    // Mapeamento ainda não validado contra o sandbox real do MP — revisar ao integrar de
    // ponta a ponta. Traduzido para o mesmo vocabulário canônico que BillingSubscriptionSyncService
    // já usa (originado do Stripe), para o switch de status continuar agnóstico de gateway.
    private static string? ToCanonicalStatus(string? mpStatus) => mpStatus?.ToLowerInvariant() switch
    {
        "authorized" => "active",
        "pending" => "incomplete",
        "paused" => "past_due",
        "cancelled" => "canceled",
        var other => other
    };
}
