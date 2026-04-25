using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/goals/income")]
[Route("api/v1/goals/income")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureGoalsManage)]
public class IncomeGoalsController(IGoalsService goalsService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? year, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var targetYear = year ?? DateTime.UtcNow.Year;
        var goal = await goalsService.GetIncomeGoalAsync(userId, targetYear, cancellationToken);
        if (goal is null) return NoContent();
        return Ok(goal);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertIncomeGoalRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var goal = await goalsService.UpsertIncomeGoalAsync(userId, request, cancellationToken);
            return Ok(goal);
        }, "Invalid goal");
    }
}
