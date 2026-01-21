using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ICardBrandRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<List<CardBrand>> ListActiveAsync(CancellationToken cancellationToken = default);
}
