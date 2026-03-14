namespace InvestindoEmNegocio.Application.DTOs;

public sealed record FinancialAssistantPromptContextResponse(
    DateOnly ReferenceDate,
    RealAvailableBalanceResponse RealBalance,
    DebtSummaryResponse Debts,
    NetWorthSummaryResponse NetWorth,
    CashflowProjectionResponse Projection,
    RiskBotAssessmentResponse Risk,
    InsightEngineResponse Insights,
    RecommendationEngineResponse Recommendations,
    string GovernanceSummary);

public sealed record FinancialAssistantChatRequest(string Question, DateOnly? ReferenceDate = null);

public sealed record FinancialAssistantChatResponse(
    bool Allowed,
    string Question,
    string Answer,
    string Disclaimer,
    string ReasonCode,
    FinancialAssistantPromptContextResponse Context);
