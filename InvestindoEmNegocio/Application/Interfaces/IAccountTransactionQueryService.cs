using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAccountTransactionQueryService
{
    Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccountTransactionResponse>?> ListTransactionsPagedAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
