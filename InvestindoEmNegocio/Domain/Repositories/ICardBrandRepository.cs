using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ICardBrandRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<List<CardBrand>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<List<CardBrand>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<CardBrand?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
