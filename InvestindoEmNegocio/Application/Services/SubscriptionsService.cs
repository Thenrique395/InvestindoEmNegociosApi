using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionsService(
    ISubscriptionCatalogService subscriptionCatalogService,
    ISubscriptionManagementService subscriptionManagementService) : ISubscriptionsService
{
    public Task<SubscriptionCatalogResponse> GetCatalogAsync(Guid userId, CancellationToken cancellationToken = default)
        => subscriptionCatalogService.GetCatalogAsync(userId, cancellationToken);

    public Task<SubscriptionChangeResponse> ChangeAsync(Guid userId, ChangeSubscriptionRequest request, CancellationToken cancellationToken = default)
        => subscriptionManagementService.ChangeAsync(userId, request, cancellationToken);

    public Task<SubscriptionChangeResponse> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
        => subscriptionManagementService.CancelAsync(userId, cancellationToken);
}
