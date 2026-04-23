using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingService(
    IBillingCheckoutCommandService billingCheckoutCommandService,
    IBillingCheckoutQueryService billingCheckoutQueryService,
    IBillingPortalService billingPortalService,
    IStripeBillingWebhookService stripeBillingWebhookService) : IBillingService
{
    public Task<StartBillingCheckoutResponse> StartCheckoutAsync(Guid userId, StartBillingCheckoutRequest request, CancellationToken cancellationToken = default)
        => billingCheckoutCommandService.StartCheckoutAsync(userId, request, cancellationToken);

    public Task<BillingCheckoutStatusResponse?> GetCheckoutStatusAsync(Guid userId, Guid checkoutId, CancellationToken cancellationToken = default)
        => billingCheckoutQueryService.GetCheckoutStatusAsync(userId, checkoutId, cancellationToken);

    public Task<BillingCheckoutStatusResponse?> GetCheckoutStatusByProviderSessionAsync(Guid userId, string providerSessionId, CancellationToken cancellationToken = default)
        => billingCheckoutQueryService.GetCheckoutStatusByProviderSessionAsync(userId, providerSessionId, cancellationToken);

    public Task<BillingPortalSessionResponse> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default)
        => billingPortalService.CreatePortalSessionAsync(userId, cancellationToken);

    public Task ProcessStripeWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
        => stripeBillingWebhookService.ProcessStripeWebhookAsync(payload, signatureHeader, cancellationToken);
}
