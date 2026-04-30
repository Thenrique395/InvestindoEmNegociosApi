using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class AccountServiceMappings
{
    public static AccountResponse MapToResponse(Account account, decimal transactionsNet)
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

    public static AccountTransactionResponse MapTransactionResponse(AccountTransaction transaction)
    {
        return new AccountTransactionResponse(
            transaction.Id,
            transaction.AccountId,
            transaction.OccurredAt,
            ResolveTransactionType(transaction),
            transaction.Kind,
            transaction.Amount,
            transaction.Description,
            transaction.SourceType,
            ResolveSourceGroup(transaction.SourceType),
            ResolveSourceLabel(transaction.SourceType),
            transaction.SourceId,
            transaction.CreatedAt);
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
