using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class GoalRepository : IGoalRepository
{
    private readonly InvestDbContext _context;

    public GoalRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<Goal>> ListByUserAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Goals.AsNoTracking().Where(g => g.UserId == userId);
        if (year.HasValue) query = query.Where(g => g.Year == year.Value);
        if (status.HasValue) query = query.Where(g => g.Status == status.Value);

        return await query.OrderByDescending(g => g.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Goals.AsNoTracking().AnyAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await _context.Goals.AddAsync(goal, cancellationToken);
    }

    public void Remove(Goal goal)
    {
        _context.Goals.Remove(goal);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
