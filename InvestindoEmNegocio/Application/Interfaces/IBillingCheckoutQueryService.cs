using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBillingCheckoutQueryService
{
    Task<BillingCheckoutStatusResponse?> GetCheckoutStatusAsync(Guid userId, Guid checkoutId, CancellationToken cancellationToken = default);
    Task<BillingCheckoutStatusResponse?> GetCheckoutStatusByProviderSessionAsync(Guid userId, string providerSessionId, CancellationToken cancellationToken = default);
}
