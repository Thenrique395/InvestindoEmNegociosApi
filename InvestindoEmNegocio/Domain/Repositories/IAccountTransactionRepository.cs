using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IAccountTransactionRepository
{
    Task<List<AccountTransaction>> ListByAccountAsync(Guid accountId, Guid userId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default);
    Task<decimal> SumSignedAmountByAccountAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(AccountTransaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
