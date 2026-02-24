using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

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
    AccountTransactionKind Kind,
    decimal Amount,
    string Description,
    string? SourceType,
    Guid? SourceId,
    DateTime CreatedAt);

public record AccountBalanceResponse(
    Guid AccountId,
    decimal InitialBalance,
    decimal TransactionsNet,
    decimal CurrentBalance);
