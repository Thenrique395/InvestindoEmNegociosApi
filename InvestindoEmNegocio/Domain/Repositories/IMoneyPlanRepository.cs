using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMoneyPlanRepository
{
    Task<List<MoneyPlan>> ListByUserAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default);
    Task<MoneyPlan?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    // Space-agnostic on purpose: a category is "in use" if any of the user's plans reference it,
    // in any space, so deleting it would not silently null out history elsewhere.
    Task<bool> ExistsByCategoryAsync(Guid categoryId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MoneyPlan plan, CancellationToken cancellationToken = default);
    void Remove(MoneyPlan plan);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
