using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IGoalContributionRepository
{
    Task<List<GoalContribution>> ListByGoalAsync(Guid goalId, Guid userId, CancellationToken cancellationToken = default, bool track = false);
    Task<Dictionary<Guid, DateOnly>> GetLastContributionDatesByGoalsAsync(Guid userId, IEnumerable<Guid> goalIds, CancellationToken cancellationToken = default);
    Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
