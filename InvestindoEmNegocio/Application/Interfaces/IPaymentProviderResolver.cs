namespace InvestindoEmNegocio.Application.Interfaces;

/// <summary>
/// Resolve qual gateway de pagamento usar com base no <see cref="Domain.Entities.UserSubscription.Provider"/>
/// (ou equivalente) de uma assinatura — necessário porque, com dois gateways simultâneos
/// (Stripe e Mercado Pago), não existe uma única instância de <see cref="IPaymentProvider"/>
/// que sirva para todas as assinaturas.
/// </summary>
public interface IPaymentProviderResolver
{
    IPaymentProvider Resolve(string? provider);
}
