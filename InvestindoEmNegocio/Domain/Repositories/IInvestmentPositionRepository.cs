using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IInvestmentPositionRepository
{
    Task<List<InvestmentPosition>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InvestmentPosition?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(InvestmentPosition position, CancellationToken cancellationToken = default);
    void Remove(InvestmentPosition position);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
