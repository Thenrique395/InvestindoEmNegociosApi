using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/investments")]
[Route("api/v1/investments")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvestmentsAccess)]
public class InvestmentGoalsController(
    IInvestmentsService investmentsService,
    IInvestmentsApplicationService investmentsApplicationService) : AuthenticatedControllerBase
{
    [HttpGet("goal")]
    public async Task<ActionResult<InvestmentGoalDto>> GetGoal(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await investmentsService.GetGoalAsync(userId, cancellationToken);
        if (goal is null) return NoContent();
        return Ok(goal);
    }

    [HttpPut("goal")]
    public async Task<ActionResult<InvestmentGoalDto>> UpsertGoal([FromBody] UpsertInvestmentGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await investmentsService.UpsertGoalAsync(userId, request, cancellationToken);
        return Ok(goal);
    }

    [HttpGet("allocation-target")]
    public async Task<ActionResult<InvestmentAllocationTargetDto>> GetAllocationTarget(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var target = await investmentsService.GetAllocationTargetAsync(userId, cancellationToken);
        return Ok(target);
    }

    [HttpPut("allocation-target")]
    public async Task<ActionResult<InvestmentAllocationTargetDto>> UpsertAllocationTarget([FromBody] UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var target = await investmentsApplicationService.UpsertAllocationTargetAsync(userId, request, cancellationToken);
        return Ok(target);
    }
}
