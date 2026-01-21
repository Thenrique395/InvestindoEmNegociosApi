using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMoneyPlanRepository
{
    Task<List<MoneyPlan>> ListByUserAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default);
    Task<MoneyPlan?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MoneyPlan plan, CancellationToken cancellationToken = default);
    void Remove(MoneyPlan plan);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
