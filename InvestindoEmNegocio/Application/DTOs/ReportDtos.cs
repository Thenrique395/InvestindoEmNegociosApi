namespace InvestindoEmNegocio.Application.DTOs;

public record MonthlySummaryReportResponse(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetBalance,
    decimal SavingsRate,
    IReadOnlyList<CategoryExpenseResponse> ExpensesByCategory,
    IReadOnlyList<CategoryExpenseResponse> TopExpenses);

public record CategoryExpenseResponse(
    string CategoryName,
    decimal Amount,
    decimal PercentageOfTotal);
