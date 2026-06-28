using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyPlanRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IMoneyPlanRepository
{
    public async Task<List<MoneyPlan>> ListByUserAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var query = context.MoneyPlans.AsNoTracking().Where(p => p.UserId == userId && (!spaceId.HasValue || p.SpaceId == spaceId.Value));
        if (type.HasValue) query = query.Where(p => p.Type == type.Value);
        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<MoneyPlan?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.MoneyPlans.FirstOrDefaultAsync(
            p => p.Id == id && p.UserId == userId && (!spaceId.HasValue || p.SpaceId == spaceId.Value),
            cancellationToken);
    }

    public async Task AddAsync(MoneyPlan plan, CancellationToken cancellationToken = default)
    {
        await context.MoneyPlans.AddAsync(plan, cancellationToken);
    }

    public void Remove(MoneyPlan plan)
    {
        context.MoneyPlans.Remove(plan);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
