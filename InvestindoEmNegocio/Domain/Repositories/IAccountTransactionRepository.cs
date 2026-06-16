using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IAccountTransactionRepository
{
    Task<List<AccountTransaction>> ListByAccountAsync(Guid accountId, Guid userId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default);
    Task<(List<AccountTransaction> Items, int TotalCount)> ListByAccountPagedAsync(Guid accountId, Guid userId, DateTime? fromUtc = null, DateTime? toUtc = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<List<AccountTransaction>> ListBySourceAsync(Guid userId, string sourceType, IEnumerable<Guid> sourceIds, CancellationToken cancellationToken = default);
    Task<decimal> SumSignedAmountByAccountAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(AccountTransaction transaction, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<AccountTransaction> transactions);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
