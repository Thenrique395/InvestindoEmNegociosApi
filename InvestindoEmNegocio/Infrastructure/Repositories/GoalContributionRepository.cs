using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class GoalContributionRepository(InvestDbContext context) : IGoalContributionRepository
{
    public async Task<List<GoalContribution>> ListByGoalAsync(Guid goalId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.GoalContributions.AsNoTracking()
            .Where(x => x.GoalId == goalId && x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, DateOnly>> GetLastContributionDatesByGoalsAsync(
        Guid userId,
        IEnumerable<Guid> goalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = goalIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0) return [];

        var grouped = await context.GoalContributions.AsNoTracking()
            .Where(x => x.UserId == userId && ids.Contains(x.GoalId))
            .GroupBy(x => x.GoalId)
            .Select(g => new { GoalId = g.Key, LastDate = g.Max(x => x.Date) })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(x => x.GoalId, x => x.LastDate);
    }

    public async Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default)
    {
        await context.GoalContributions.AddAsync(contribution, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
