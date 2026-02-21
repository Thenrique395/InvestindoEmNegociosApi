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

    public async Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default)
    {
        await context.GoalContributions.AddAsync(contribution, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
