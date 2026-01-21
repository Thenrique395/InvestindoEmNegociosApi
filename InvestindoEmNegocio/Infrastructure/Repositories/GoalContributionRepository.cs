using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class GoalContributionRepository : IGoalContributionRepository
{
    private readonly InvestDbContext _context;

    public GoalContributionRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<GoalContribution>> ListByGoalAsync(Guid goalId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.GoalContributions.AsNoTracking()
            .Where(x => x.GoalId == goalId && x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(GoalContribution contribution, CancellationToken cancellationToken = default)
    {
        await _context.GoalContributions.AddAsync(contribution, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
