using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ISubscriptionCatalogService
{
    Task<SubscriptionCatalogResponse> GetCatalogAsync(Guid userId, CancellationToken cancellationToken = default);
}
