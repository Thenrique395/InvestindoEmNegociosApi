using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IGoalRepository
{
    Task<List<Goal>> ListByUserAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default);
    Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Goal goal, CancellationToken cancellationToken = default);
    void Remove(Goal goal);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
