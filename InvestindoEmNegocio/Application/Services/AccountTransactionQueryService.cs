using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class AccountTransactionQueryService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository) : IAccountTransactionQueryService
{
    public async Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var items = await accountTransactionRepository.ListByAccountAsync(accountId, userId, fromUtc, toUtc, cancellationToken);
        return items.Select(AccountServiceMappings.MapTransactionResponse).ToList();
    }

    public async Task<PagedResult<AccountTransactionResponse>?> ListTransactionsPagedAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var (items, totalCount) = await accountTransactionRepository.ListByAccountPagedAsync(
            accountId, userId, fromUtc, toUtc, page, pageSize, cancellationToken);

        return new PagedResult<AccountTransactionResponse>(
            items.Select(AccountServiceMappings.MapTransactionResponse).ToList(),
            totalCount,
            page,
            pageSize);
    }
}
