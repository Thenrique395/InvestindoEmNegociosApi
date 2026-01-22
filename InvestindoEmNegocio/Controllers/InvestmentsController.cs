using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class InvestmentsController(IInvestmentsService investmentsService, IAuditService auditService) : ControllerBase
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

    [HttpGet("positions")]
    public async Task<IActionResult> ListPositions([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await investmentsService.ListPositionsAsync(userId, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<InvestmentPositionDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["asset"] = x => x.Asset,
                ["type"] = x => x.Type,
                ["quantity"] = x => x.Quantity,
                ["avgPrice"] = x => x.AvgPrice,
                ["openedAt"] = x => x.OpenedAt,
                ["account"] = x => x.Account,
                ["category"] = x => x.Category
            });

        if (isPaged)
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(items);
    }

    [HttpGet("positions/{id:guid}")]
    public async Task<IActionResult> GetPosition(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsService.GetPositionAsync(userId, id, cancellationToken);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [HttpPost("positions")]
    public async Task<IActionResult> CreatePosition([FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var position = await investmentsService.CreatePositionAsync(userId, request, cancellationToken);
            return Ok(position);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Posição inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("positions/{id:guid}")]
    public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var position = await investmentsService.UpdatePositionAsync(userId, id, request, cancellationToken);
            if (position is null) return NotFound();
            return Ok(position);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Posição inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpDelete("positions/{id:guid}")]
    public async Task<IActionResult> DeletePosition(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await investmentsService.DeletePositionAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "InvestmentPosition", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

    [HttpPost("positions/{id:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid id, [FromBody] CreateInvestmentMovementRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var movement = await investmentsService.AddMovementAsync(userId, id, request, cancellationToken);
            return Ok(movement);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Movimento inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");
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
