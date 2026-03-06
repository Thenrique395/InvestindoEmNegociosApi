using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAccountsService
{
    Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);
    Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(Guid userId, Guid accountId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default);
    Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default);
}
