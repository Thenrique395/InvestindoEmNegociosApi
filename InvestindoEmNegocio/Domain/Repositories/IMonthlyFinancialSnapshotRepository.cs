using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMonthlyFinancialSnapshotRepository
{
    Task<MonthlyFinancialSnapshot?> GetByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<List<MonthlyFinancialSnapshot>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MonthlyFinancialSnapshot snapshot, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
