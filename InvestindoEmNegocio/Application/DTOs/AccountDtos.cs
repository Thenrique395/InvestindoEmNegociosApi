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

public record RealAvailableBalanceResponse(
    string Period,
    DateOnly ReferenceDate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ActiveAccountsBalance,
    decimal PendingExpensesAmount,
    int PendingExpensesCount,
    decimal PendingIncomesAmount,
    int PendingIncomesCount,
    decimal RealAvailableBalance,
    decimal ProjectedAvailableBalance,
    decimal OverdueExpensesAmount,
    int OverdueExpensesCount,
    decimal DueSoonExpensesAmount);

public record CashflowProjectionPointResponse(
    DateOnly Date,
    decimal OpeningBalance,
    decimal IncomesAmount,
    int IncomesCount,
    decimal ExpensesAmount,
    int ExpensesCount,
    decimal ClosingBalance);

public record CashflowProjectionResponse(
    string Period,
    DateOnly ReferenceDate,
    DateOnly ProjectionStart,
    DateOnly ProjectionEnd,
    decimal OpeningBalance,
    decimal ProjectedClosingBalance,
    decimal LowestBalance,
    DateOnly? LowestBalanceDate,
    DateOnly? RiskDate,
    IReadOnlyList<CashflowProjectionPointResponse> Points);

public record RiskBotRecommendationResponse(
    string Id,
    string Severity,
    string Text,
    string ActionLabel,
    string Route,
    IReadOnlyDictionary<string, string> QueryParams,
    decimal? Amount,
    DateOnly? DueDate);

public record RiskBotAssessmentResponse(
    string Period,
    DateOnly ReferenceDate,
    int Score,
    string Classification,
    string Priority,
    DateOnly? RiskDate,
    decimal CurrentCoverage,
    decimal ProjectedCoverage,
    decimal ProjectedBalance,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> ScoreBreakdown,
    IReadOnlyList<RiskBotRecommendationResponse> Recommendations);

public record InsightEngineItemResponse(
    string Family,
    string Scenario,
    string Priority,
    string Title,
    string Message,
    string Action,
    int HealthScore,
    DateOnly? RiskDate,
    decimal CurrentCoverage,
    decimal ProjectedCoverage,
    decimal ProjectedBalance,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Tips,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> ScoreBreakdown,
    IReadOnlyList<RiskBotRecommendationResponse> Recommendations);

public record InsightEngineResponse(
    string Period,
    DateOnly ReferenceDate,
    InsightEngineItemResponse? PrimaryInsight,
    IReadOnlyList<InsightEngineItemResponse> Insights);

public record RecommendationItemResponse(
    string Id,
    int Score,
    string Severity,
    string Source,
    string Title,
    string Text,
    string ActionLabel,
    string Route,
    IReadOnlyDictionary<string, string> QueryParams,
    IReadOnlyList<string> ReasonCodes,
    decimal? Amount,
    DateOnly? DueDate);

public record RecommendationEngineResponse(
    string Period,
    DateOnly ReferenceDate,
    int MinScoreApplied,
    IReadOnlyList<RecommendationItemResponse> Items);

public record WealthAssetBreakdownResponse(
    decimal AccountsBalance,
    decimal InvestmentsBalance,
    decimal TotalAssets);

public record WealthLiabilityBreakdownResponse(
    decimal CardDebt,
    decimal OtherOpenLiabilities,
    decimal TotalLiabilities);

public record NetWorthSummaryResponse(
    DateOnly ReferenceDate,
    WealthAssetBreakdownResponse Assets,
    WealthLiabilityBreakdownResponse Liabilities,
    decimal NetWorth,
    int InvestmentPositionsCount,
    int OpenLiabilitiesCount,
    string SnapshotLabel);

public record NetWorthHistoryPointResponse(
    DateOnly ReferenceDate,
    string Label,
    decimal AccountsBalance,
    decimal InvestmentsBalance,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal NetWorth,
    bool IsEstimated);

public record NetWorthHistoryResponse(
    DateOnly ReferenceDate,
    int Months,
    bool HasEstimatedPoints,
    IReadOnlyList<string> Notes,
    IReadOnlyList<NetWorthHistoryPointResponse> Points);

public record DebtSummaryBucketResponse(
    string Key,
    string Label,
    decimal TotalAmount,
    int ItemsCount);

public record DebtSummaryItemResponse(
    Guid InstallmentId,
    Guid PlanId,
    string Family,
    string Title,
    string? RelatedName,
    DateOnly DueDate,
    decimal OriginalAmount,
    decimal PaidAmount,
    decimal OpenAmount,
    string Status,
    string? StatementReference);

public record DebtSummaryResponse(
    DateOnly ReferenceDate,
    decimal TotalDebt,
    decimal CardDebt,
    decimal OtherDebt,
    decimal OverdueDebt,
    decimal DueSoonDebt,
    int OpenItemsCount,
    IReadOnlyList<DebtSummaryBucketResponse> Buckets,
    IReadOnlyList<DebtSummaryItemResponse> NextItems);

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
