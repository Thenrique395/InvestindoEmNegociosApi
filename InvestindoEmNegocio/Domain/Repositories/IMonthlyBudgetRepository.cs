using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMonthlyBudgetRepository
{
    Task<List<MonthlyBudgetItem>> ListByMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<MonthlyBudgetItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MonthlyBudgetItem item, CancellationToken cancellationToken = default);
    void Remove(MonthlyBudgetItem item);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
