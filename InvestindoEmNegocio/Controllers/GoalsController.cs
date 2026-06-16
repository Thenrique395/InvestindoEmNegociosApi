using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/goals")]
[Route("api/v1/goals")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureGoalsManage)]
public class GoalsController(IGoalsService goalsService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet]
    // Lists user goals with optional year and status filters.
    public async Task<IActionResult> List([FromQuery] int? year, [FromQuery] GoalStatus? status, [FromQuery] ListQuery query, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await goalsService.ListAsync(userId, year, status, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<GoalResponse, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["createdAt"] = x => x.CreatedAt,
                ["updatedAt"] = x => x.UpdatedAt,
                ["title"] = x => x.Title,
                ["targetAmount"] = x => x.TargetAmount,
                ["currentAmount"] = x => x.CurrentAmount,
                ["year"] = x => x.Year,
                ["status"] = x => x.Status,
                ["targetDate"] = x => x.TargetDate
            });

        if (isPaged)
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    // Returns a single goal owned by the current user.
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await goalsService.GetByIdAsync(userId, id, cancellationToken);
        if (goal is null) return NotFound();
        return Ok(goal);
    }

    [HttpPost]
    // Creates a yearly goal for the current user.
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var goal = await goalsService.CreateAsync(userId, request, cancellationToken);
            return Ok(goal);
        }, "Meta inválida");
    }

    [HttpPut("{id:guid}")]
    // Updates a goal owned by the current user.
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var goal = await goalsService.UpdateAsync(userId, id, request, cancellationToken);
            if (goal is null) return NotFound();
            return Ok(goal);
        }, "Meta inválida");
    }

    [HttpDelete("{id:guid}")]
    // Deletes a goal owned by the current user.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await goalsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Goal", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

}
