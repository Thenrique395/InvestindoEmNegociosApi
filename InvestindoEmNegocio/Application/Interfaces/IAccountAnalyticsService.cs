using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAccountAnalyticsService
{
    Task<RealAvailableBalanceResponse> GetRealAvailableBalanceAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<DebtSummaryResponse> GetDebtSummaryAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<SubscriptionsSummaryResponse> GetSubscriptionsSummaryAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<NetWorthSummaryResponse> GetNetWorthSummaryAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<NetWorthHistoryResponse> GetNetWorthHistoryAsync(Guid userId, int months = 12, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<CashflowProjectionResponse> GetProjectionAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<RiskBotAssessmentResponse> GetRiskAssessmentAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<InsightEngineResponse> GetInsightsAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<RecommendationEngineResponse> GetRecommendationsAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
}
