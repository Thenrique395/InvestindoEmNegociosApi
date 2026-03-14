using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ISubscriptionsService
{
    Task<SubscriptionCatalogResponse> GetCatalogAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionChangeResponse> ChangeAsync(Guid userId, ChangeSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionChangeResponse> CancelAsync(Guid userId, CancellationToken cancellationToken = default);
}
