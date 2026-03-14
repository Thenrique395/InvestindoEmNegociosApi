using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MonthlyFinancialSnapshotRepository(InvestDbContext context) : IMonthlyFinancialSnapshotRepository
{
    public async Task<MonthlyFinancialSnapshot?> GetByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
        => await context.MonthlyFinancialSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Year == year && x.Month == month, cancellationToken);

    public async Task<List<MonthlyFinancialSnapshot>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.MonthlyFinancialSnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MonthlyFinancialSnapshot snapshot, CancellationToken cancellationToken = default)
        => await context.MonthlyFinancialSnapshots.AddAsync(snapshot, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
