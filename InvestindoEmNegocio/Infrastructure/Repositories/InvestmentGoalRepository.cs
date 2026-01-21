using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InvestmentGoalRepository : IInvestmentGoalRepository
{
    private readonly InvestDbContext _context;

    public InvestmentGoalRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<InvestmentGoal?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.InvestmentGoals.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(InvestmentGoal goal, CancellationToken cancellationToken = default)
    {
        await _context.InvestmentGoals.AddAsync(goal, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
