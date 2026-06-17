using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public class AccountsService(
    IAccountQueryService accountQueryService,
    IAccountCommandService accountCommandService,
    IAccountTransactionQueryService accountTransactionQueryService,
    IAccountTransferService accountTransferService) : IAccountsService
{
    public Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        accountQueryService.ListAsync(userId, cancellationToken);

    public Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default) =>
        accountCommandService.CreateAsync(userId, request, cancellationToken);

    public Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default) =>
        accountCommandService.UpdateAsync(userId, accountId, request, cancellationToken);

    public Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
        accountCommandService.DeleteAsync(userId, accountId, cancellationToken);

    public Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
        accountQueryService.GetBalanceAsync(userId, accountId, cancellationToken);

    public Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default) =>
        accountTransactionQueryService.ListTransactionsAsync(userId, accountId, fromUtc, toUtc, cancellationToken);

    public Task<PagedResult<AccountTransactionResponse>?> ListTransactionsPagedAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        accountTransactionQueryService.ListTransactionsPagedAsync(userId, accountId, fromUtc, toUtc, page, pageSize, cancellationToken);

    public Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default) =>
        accountTransferService.TransferAsync(userId, request, cancellationToken);
}
