namespace InvestindoEmNegocio.Application.DTOs;

public sealed record MonthlyFinancialSnapshotResponse(
    Guid Id,
    int Year,
    int Month,
    string SnapshotLabel,
    decimal RealAvailableBalance,
    decimal ProjectedBalance,
    decimal PendingExpenses,
    decimal PendingIncomes,
    decimal TotalDebt,
    decimal NetWorth,
    int RiskScore,
    string RiskClassification,
    string PrimaryInsight,
    IReadOnlyList<string> Recommendations,
    DateTime CreatedAt);

public sealed record GenerateMonthlyFinancialSnapshotRequest(int Year, int Month);
