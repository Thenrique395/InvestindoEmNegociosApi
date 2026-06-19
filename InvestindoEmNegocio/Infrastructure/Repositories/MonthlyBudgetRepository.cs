using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MonthlyBudgetRepository(InvestDbContext context) : IMonthlyBudgetRepository
{
    public async Task<List<MonthlyBudgetItem>> ListByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
        => await context.MonthlyBudgetItems
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Year == year && x.Month == month)
            .OrderBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);

    public async Task<MonthlyBudgetItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => await context.MonthlyBudgetItems
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public async Task AddAsync(MonthlyBudgetItem item, CancellationToken cancellationToken = default)
        => await context.MonthlyBudgetItems.AddAsync(item, cancellationToken);

    public void Remove(MonthlyBudgetItem item)
        => context.MonthlyBudgetItems.Remove(item);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
