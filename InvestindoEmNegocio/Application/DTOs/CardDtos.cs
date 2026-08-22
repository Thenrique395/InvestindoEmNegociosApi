namespace InvestindoEmNegocio.Application.DTOs;

public record CardRequest(
    int BrandId,
    string? HolderName,
    string Last4,
    string? Nickname,
    string? Bank,
    decimal CreditLimit,
    int StatementCloseDay,
    int DueDay);

public record CardResponse(
    Guid Id,
    int BrandId,
    string HolderName,
    string Nickname,
    string Last4,
    string? Bank,
    decimal CreditLimit,
    int StatementCloseDay,
    int DueDay,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CardStatementInstallmentResponse(
    Guid InstallmentId,
    Guid PlanId,
    string Title,
    int InstallmentNo,
    DateOnly PurchaseDate,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal OpenAmount,
    string Status);

public record CardStatementCycleResponse(
    int StatementYear,
    int StatementMonth,
    DateOnly StatementCloseDate,
    DateOnly StatementDueDate,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal TotalOpen,
    int ItemsCount,
    IReadOnlyList<CardStatementInstallmentResponse> Items);
