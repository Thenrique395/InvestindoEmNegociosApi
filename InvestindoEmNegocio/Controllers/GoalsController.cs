using System;
using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class GoalsController(IGoalsService goalsService, IAuditService auditService) : ControllerBase
{
    [HttpGet("income")]
    public async Task<IActionResult> GetIncomeGoal([FromQuery] int? year, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var targetYear = year ?? DateTime.UtcNow.Year;
        var goal = await goalsService.GetIncomeGoalAsync(userId, targetYear, cancellationToken);
        if (goal is null) return NoContent();
        return Ok(goal);
    }

    [HttpPut("income")]
    public async Task<IActionResult> UpsertIncomeGoal([FromBody] UpsertIncomeGoalRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var goal = await goalsService.UpsertIncomeGoalAsync(userId, request, cancellationToken);
            return Ok(goal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Meta inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpGet]
    // Lista metas do usuário, opcionalmente filtrando por ano ou status.
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
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    // Retorna uma meta específica do usuário.
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await goalsService.GetByIdAsync(userId, id, cancellationToken);
        if (goal is null) return NotFound();
        return Ok(goal);
    }

    [HttpPost]
    // Cria uma meta anual para o usuário.
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var goal = await goalsService.CreateAsync(userId, request, cancellationToken);
            return Ok(goal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Meta inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
    // Atualiza uma meta do usuário.
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var goal = await goalsService.UpdateAsync(userId, id, request, cancellationToken);
            if (goal is null) return NotFound();
            return Ok(goal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Meta inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpDelete("{id:guid}")]
    // Remove uma meta do usuário.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await goalsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Goal", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
