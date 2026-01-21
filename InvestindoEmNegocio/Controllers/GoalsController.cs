using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoalsController(IGoalsService goalsService) : ControllerBase
{
    [HttpGet]
    // Lista metas do usuário, opcionalmente filtrando por ano ou status.
    public async Task<IActionResult> List([FromQuery] int? year, [FromQuery] GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await goalsService.ListAsync(userId, year, status, cancellationToken);
        return Ok(data);
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
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
