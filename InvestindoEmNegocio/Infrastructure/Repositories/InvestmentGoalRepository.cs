using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InvestmentGoalRepository(InvestDbContext context) : IInvestmentGoalRepository
{
    public async Task<InvestmentGoal?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.InvestmentGoals.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(InvestmentGoal goal, CancellationToken cancellationToken = default)
    {
        await context.InvestmentGoals.AddAsync(goal, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
