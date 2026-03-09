using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class RecommendationEngineService(
    IRiskBotService riskBotService,
    IInsightEngineService insightEngineService) : IRecommendationEngineService
{
    private const int MinScore = 50;

    public async Task<RecommendationEngineResponse> BuildAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var risk = await riskBotService.AssessAsync(userId, period, referenceDate, cancellationToken);
        var insights = await insightEngineService.BuildAsync(userId, period, referenceDate, cancellationToken);

        var candidates = new List<RecommendationItemResponse>();
        candidates.AddRange(BuildFromRisk(risk));
        candidates.AddRange(BuildFromInsights(insights));

        var items = candidates
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Amount ?? 0m)
                .First())
            .Where(item => item.Score >= MinScore)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Amount ?? 0m)
            .Take(6)
            .ToList();

        return new RecommendationEngineResponse(
            risk.Period,
            risk.ReferenceDate,
            MinScore,
            items);
    }

    private static IEnumerable<RecommendationItemResponse> BuildFromRisk(RiskBotAssessmentResponse risk)
    {
        foreach (var item in risk.Recommendations)
        {
            yield return new RecommendationItemResponse(
                item.Id,
                ComputeRiskScore(item),
                item.Severity,
                "risk-bot",
                BuildTitleFromSeverity(item.Severity),
                item.Text,
                item.ActionLabel,
                item.Route,
                item.QueryParams,
                risk.ReasonCodes,
                item.Amount,
                item.DueDate);
        }
    }

    private static IEnumerable<RecommendationItemResponse> BuildFromInsights(InsightEngineResponse insights)
    {
        foreach (var insight in insights.Insights)
        {
            foreach (var recommendation in insight.Recommendations)
            {
                yield return new RecommendationItemResponse(
                    $"{insight.Family}:{recommendation.Id}",
                    ComputeInsightScore(insight, recommendation),
                    recommendation.Severity,
                    $"insight:{insight.Family}",
                    insight.Title,
                    recommendation.Text,
                    recommendation.ActionLabel,
                    recommendation.Route,
                    recommendation.QueryParams,
                    insight.ReasonCodes,
                    recommendation.Amount,
                    recommendation.DueDate);
            }
        }
    }

    private static int ComputeRiskScore(RiskBotRecommendationResponse item)
    {
        var baseScore = item.Severity switch
        {
            "danger" => 95,
            "warn" => 78,
            _ => 55
        };

        if ((item.Amount ?? 0m) >= 1000m) baseScore += 5;
        return Math.Min(baseScore, 100);
    }

    private static int ComputeInsightScore(InsightEngineItemResponse insight, RiskBotRecommendationResponse item)
    {
        var familyBoost = insight.Family switch
        {
            "reactive" => 12,
            "preventive" => 8,
            "structural" => 6,
            "patrimonial" => 4,
            _ => 0
        };
        var severityBase = item.Severity switch
        {
            "danger" => 82,
            "warn" => 70,
            _ => 52
        };
        return Math.Min(severityBase + familyBoost, 100);
    }

    private static string BuildTitleFromSeverity(string severity)
    {
        return severity switch
        {
            "danger" => "Ação imediata",
            "warn" => "Ação recomendada",
            _ => "Sugestão prática"
        };
    }
}
