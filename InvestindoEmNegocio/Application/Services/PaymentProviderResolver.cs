using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class PaymentProviderResolver(
    IStripeBillingGateway stripeBillingGateway,
    IMercadoPagoBillingGateway mercadoPagoBillingGateway) : IPaymentProviderResolver
{
    public IPaymentProvider Resolve(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        "mercado_pago" => (IPaymentProvider)mercadoPagoBillingGateway,
        _ => (IPaymentProvider)stripeBillingGateway
    };
}
