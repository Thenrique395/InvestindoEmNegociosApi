using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IInvestmentGoalRepository
{
    Task<InvestmentGoal?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(InvestmentGoal goal, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
