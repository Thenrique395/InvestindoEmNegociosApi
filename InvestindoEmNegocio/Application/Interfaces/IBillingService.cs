using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBillingService
{
    Task<StartBillingCheckoutResponse> StartCheckoutAsync(Guid userId, StartBillingCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<BillingCheckoutStatusResponse?> GetCheckoutStatusAsync(Guid userId, Guid checkoutId, CancellationToken cancellationToken = default);
    Task<BillingCheckoutStatusResponse?> GetCheckoutStatusByProviderSessionAsync(Guid userId, string providerSessionId, CancellationToken cancellationToken = default);
    Task<BillingPortalSessionResponse> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ProcessStripeWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default);
}
