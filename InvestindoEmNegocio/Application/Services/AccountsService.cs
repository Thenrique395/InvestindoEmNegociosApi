using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class AccountsService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ILogger<AccountsService> logger) : IAccountsService
{
    private readonly ILogger<AccountsService> _logger = logger;

    public async Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        var responses = new List<AccountResponse>(accounts.Count);

        foreach (var account in accounts)
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            responses.Add(MapToResponse(account, net));
        }

        return responses;
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        if (await accountRepository.ExistsByNameAsync(userId, request.Name, null, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        var account = new Account(userId, request.Name, request.Type, request.InitialBalance);
        if (!request.IsActive) account.Deactivate();

        await accountRepository.AddAsync(account, cancellationToken);
        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account created {UserId} {AccountId}", userId, account.Id);
        return MapToResponse(account, 0m);
    }

    public async Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        if (await accountRepository.ExistsByNameAsync(userId, request.Name, accountId, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        account.Update(request.Name, request.Type, request.InitialBalance);
        if (request.IsActive) account.Activate();
        else account.Deactivate();

        await accountRepository.SaveChangesAsync(cancellationToken);

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
        _logger.LogInformation("Account updated {UserId} {AccountId}", userId, account.Id);
        return MapToResponse(account, net);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return false;

        accountRepository.Remove(account);
        await accountRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Account deleted {UserId} {AccountId}", userId, accountId);
        return true;
    }

    public async Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(accountId, userId, cancellationToken);
        return new AccountBalanceResponse(accountId, account.InitialBalance, net, account.InitialBalance + net);
    }

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
        return items.Select(t => new AccountTransactionResponse(
            t.Id,
            t.AccountId,
            t.OccurredAt,
            t.Kind,
            t.Amount,
            t.Description,
            t.SourceType,
            t.SourceId,
            t.CreatedAt)).ToList();
    }

    private static AccountResponse MapToResponse(Account account, decimal transactionsNet)
    {
        var current = account.InitialBalance + transactionsNet;
        return new AccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.InitialBalance,
            current,
            account.IsActive,
            account.CreatedAt,
            account.UpdatedAt);
    }
}
