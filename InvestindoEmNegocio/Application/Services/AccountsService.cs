using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
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
            ResolveTransactionType(t),
            t.Kind,
            t.Amount,
            t.Description,
            t.SourceType,
            ResolveSourceGroup(t.SourceType),
            ResolveSourceLabel(t.SourceType),
            t.SourceId,
            t.CreatedAt)).ToList();
    }

    public async Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Valor da transferência deve ser maior que zero.");
        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Conta de origem e destino devem ser diferentes.");

        var from = await accountRepository.GetByIdAsync(request.FromAccountId, userId, cancellationToken);
        var to = await accountRepository.GetByIdAsync(request.ToAccountId, userId, cancellationToken);
        if (from is null || to is null) return null;
        if (!from.IsActive) throw new ArgumentException("Conta de origem está inativa.");
        if (!to.IsActive) throw new ArgumentException("Conta de destino está inativa.");

        var occurredAt = (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime();
        var transferId = Guid.NewGuid();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transferência {from.Name} -> {to.Name}"
            : request.Description.Trim();

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            from.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Debit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            to.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Credit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account transfer created {UserId} {TransferId} {FromAccountId} -> {ToAccountId} Amount {Amount}",
            userId,
            transferId,
            from.Id,
            to.Id,
            request.Amount);

        return new AccountTransferResponse(
            transferId,
            from.Id,
            to.Id,
            request.Amount,
            occurredAt,
            description);
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

    private static AccountTransactionType ResolveTransactionType(AccountTransaction transaction)
    {
        if (string.Equals(transaction.SourceType, AccountTransactionSourceTypes.AccountTransfer, StringComparison.OrdinalIgnoreCase))
            return AccountTransactionType.Transfer;

        return transaction.Kind == Domain.Enums.AccountTransactionKind.Credit
            ? AccountTransactionType.Income
            : AccountTransactionType.Expense;
    }

    private static string? ResolveSourceGroup(string? sourceType)
    {
        return sourceType switch
        {
            AccountTransactionSourceTypes.InstallmentPayment => "FinancialEntry",
            AccountTransactionSourceTypes.InstallmentPaymentReversal => "FinancialEntryReversal",
            AccountTransactionSourceTypes.AccountTransfer => "Transfer",
            AccountTransactionSourceTypes.BankStatementImport => "Import",
            _ => string.IsNullOrWhiteSpace(sourceType) ? null : "Other"
        };
    }

    private static string? ResolveSourceLabel(string? sourceType)
    {
        return sourceType switch
        {
            AccountTransactionSourceTypes.InstallmentPayment => "Receita/Despesa",
            AccountTransactionSourceTypes.InstallmentPaymentReversal => "Estorno",
            AccountTransactionSourceTypes.AccountTransfer => "Transferência",
            AccountTransactionSourceTypes.BankStatementImport => "Importação de extrato",
            _ => string.IsNullOrWhiteSpace(sourceType) ? null : sourceType
        };
    }

}
