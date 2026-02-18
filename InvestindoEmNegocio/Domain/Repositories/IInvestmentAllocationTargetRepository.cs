using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IInvestmentAllocationTargetRepository
{
    Task<InvestmentAllocationTarget?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(InvestmentAllocationTarget target, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
