using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts/summary")]
[Route("api/v1/accounts/summary")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsAnalytics)]
public class AccountInsightsController(IAccountAnalyticsService accountAnalyticsService) : AuthenticatedControllerBase
{
    [HttpGet("risk")]
    public async Task<ActionResult<RiskBotAssessmentResponse>> Risk(
        [FromQuery] string? period,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<RiskBotAssessmentResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetRiskAssessmentAsync(userId, period ?? "month", referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid period");
    }

    [HttpGet("insights")]
    public async Task<ActionResult<InsightEngineResponse>> Insights(
        [FromQuery] string? period,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<InsightEngineResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetInsightsAsync(userId, period ?? "month", referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid period");
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<RecommendationEngineResponse>> Recommendations(
        [FromQuery] string? period,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<RecommendationEngineResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetRecommendationsAsync(userId, period ?? "month", referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid period");
    }
}
