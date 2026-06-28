using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class GoalRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IGoalRepository
{
    public async Task<List<Goal>> ListByUserAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var query = context.Goals.AsNoTracking().Where(g => g.UserId == userId && (!spaceId.HasValue || g.SpaceId == spaceId.Value));
        if (year.HasValue) query = query.Where(g => g.Year == year.Value);
        if (status.HasValue) query = query.Where(g => g.Status == status.Value);

        return await query.OrderByDescending(g => g.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId && (!spaceId.HasValue || g.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Goals.AsNoTracking().AnyAsync(g => g.Id == id && g.UserId == userId && (!spaceId.HasValue || g.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task AddAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await context.Goals.AddAsync(goal, cancellationToken);
    }

    public void Remove(Goal goal)
    {
        context.Goals.Remove(goal);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
