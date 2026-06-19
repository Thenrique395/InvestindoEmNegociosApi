namespace InvestindoEmNegocio.Application.DTOs;

public record BudgetItemRequest(string CategoryName, decimal PlannedAmount);

public record BudgetItemResponse(
    Guid Id,
    string CategoryName,
    decimal PlannedAmount,
    decimal RealizedAmount,
    decimal Variance);

public record BudgetResponse(
    int Year,
    int Month,
    decimal TotalPlanned,
    decimal TotalRealized,
    decimal TotalVariance,
    IReadOnlyList<BudgetItemResponse> Items);
