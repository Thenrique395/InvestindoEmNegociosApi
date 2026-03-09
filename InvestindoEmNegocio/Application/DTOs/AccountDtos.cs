using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public enum AccountTransactionType
{
    Income,
    Expense,
    Transfer
}

public record AccountRequest(
    string Name,
    AccountType Type,
    decimal InitialBalance = 0m,
    bool IsActive = true);

public record AccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    decimal InitialBalance,
    decimal CurrentBalance,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AccountTransactionResponse(
    Guid Id,
    Guid AccountId,
    DateTime OccurredAt,
    AccountTransactionType Type,
    AccountTransactionKind Kind,
    decimal Amount,
    string Description,
    string? SourceType,
    string? SourceGroup,
    string? SourceLabel,
    Guid? SourceId,
    DateTime CreatedAt);

public record AccountBalanceResponse(
    Guid AccountId,
    decimal InitialBalance,
    decimal TransactionsNet,
    decimal CurrentBalance);

public record AccountTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime? OccurredAt = null,
    string? Description = null);

public record AccountTransferResponse(
    Guid TransferId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime OccurredAtUtc,
    string Description);
