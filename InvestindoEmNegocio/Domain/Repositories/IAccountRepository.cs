using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IAccountRepository
{
    Task<List<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid userId, string name, Guid? ignoreId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    void Remove(Account account);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
