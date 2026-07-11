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
public class GoalsController(IGoalsService goalsService, IGoalProgressService goalProgressService, IGoalOccurrenceService goalOccurrenceService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet("{id:guid}/progress")]
    // Returns the calculated progress (from real transactions) of a goal owned by the user.
    public async Task<IActionResult> GetProgress(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var progress = await goalProgressService.GetProgressAsync(userId, id, cancellationToken);
        if (progress is null) return NotFound();
        return Ok(progress);
    }

    [HttpGet("{id:guid}/occurrences")]
    // Returns the occurrences (recurring periods) of a goal, with realized per period.
    public async Task<IActionResult> GetOccurrences(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var occurrences = await goalOccurrenceService.EnsureAndListAsync(userId, id, cancellationToken);
        if (occurrences is null) return NotFound();
        return Ok(occurrences);
    }

    [HttpGet("{id:guid}/history")]
    // History = occurrences of the goal (period-by-period evolution).
    public Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken) => GetOccurrences(id, cancellationToken);

    [HttpPut("{id:guid}/occurrences/current")]
    // Edits only the current occurrence target (not the whole series).
    public async Task<IActionResult> OverrideCurrentOccurrence(Guid id, [FromBody] OverrideOccurrenceRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var updated = await goalOccurrenceService.OverrideCurrentTargetAsync(userId, id, request.TargetAmount, cancellationToken);
            if (!updated) return NotFound();
            return NoContent();
        }, "Ocorrência inválida");
    }

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
            return Created("", goal);
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

    [HttpPost("{id:guid}/pause")]
    public Task<IActionResult> Pause(Guid id, CancellationToken ct) => Transition(id, goalsService.PauseAsync, ct);

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct) => Transition(id, goalsService.ResumeAsync, ct);

    [HttpPost("{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => Transition(id, goalsService.ArchiveAsync, ct);

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct) => Transition(id, goalsService.CompleteAsync, ct);

    private async Task<IActionResult> Transition(Guid id, Func<Guid, Guid, CancellationToken, Task<GoalResponse?>> action, CancellationToken ct)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var goal = await action(userId, id, ct);
            if (goal is null) return NotFound();
            return Ok(goal);
        }, "Transição de meta inválida");
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
