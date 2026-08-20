using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class PlanHistoryRepository(InvestDbContext context) : IPlanHistoryRepository
{
    public async Task AddAsync(PlanHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await context.PlanHistoryEntries.AddAsync(entry, cancellationToken);
    }

    public async Task<List<PlanHistoryEntry>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.PlanHistoryEntries.AsNoTracking()
            .Where(e => e.PlanId == planId && e.UserId == userId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
