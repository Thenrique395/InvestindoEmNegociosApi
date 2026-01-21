using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ICategoryRepository
{
    Task<List<Category>> ListForUserAsync(Guid userId, MoneyType? appliesTo, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    void Remove(Category category);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
