using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MonthlyBudgetRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IMonthlyBudgetRepository
{
    public async Task<List<MonthlyBudgetItem>> ListByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.RequireSpaceId();
        return await context.MonthlyBudgetItems
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.SpaceId == spaceId && x.Year == year && x.Month == month)
            .OrderBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MonthlyBudgetItem>> ListForUpdateByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.RequireSpaceId();
        return await context.MonthlyBudgetItems
            .Where(x => x.UserId == userId && x.SpaceId == spaceId && x.Year == year && x.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<MonthlyBudgetItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.RequireSpaceId();
        return await context.MonthlyBudgetItems
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.SpaceId == spaceId, cancellationToken);
    }

    public async Task AddAsync(MonthlyBudgetItem item, CancellationToken cancellationToken = default)
        => await context.MonthlyBudgetItems.AddAsync(item, cancellationToken);

    public void Remove(MonthlyBudgetItem item)
        => context.MonthlyBudgetItems.Remove(item);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
