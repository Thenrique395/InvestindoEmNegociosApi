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
public class AccountSummariesController(IAccountAnalyticsService accountAnalyticsService) : AuthenticatedControllerBase
{
    [HttpGet("real-balance")]
    public async Task<ActionResult<RealAvailableBalanceResponse>> RealBalance(
        [FromQuery] string? period,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<RealAvailableBalanceResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetRealAvailableBalanceAsync(userId, period ?? "month", referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid period");
    }

    [HttpGet("debts")]
    public async Task<ActionResult<DebtSummaryResponse>> Debts(
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var summary = await accountAnalyticsService.GetDebtSummaryAsync(userId, referenceDate, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("net-worth")]
    public async Task<ActionResult<NetWorthSummaryResponse>> NetWorth(
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var summary = await accountAnalyticsService.GetNetWorthSummaryAsync(userId, referenceDate, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("net-worth/history")]
    public async Task<ActionResult<NetWorthHistoryResponse>> NetWorthHistory(
        [FromQuery] int? months,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<NetWorthHistoryResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetNetWorthHistoryAsync(userId, months ?? 12, referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid parameter");
    }

    [HttpGet("projection")]
    public async Task<ActionResult<CashflowProjectionResponse>> Projection(
        [FromQuery] string? period,
        [FromQuery] DateOnly? referenceDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<CashflowProjectionResponse>(async () =>
        {
            var userId = GetUserId();
            var summary = await accountAnalyticsService.GetProjectionAsync(userId, period ?? "month", referenceDate, cancellationToken);
            return Ok(summary);
        }, "Invalid period");
    }

}
