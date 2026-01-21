using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/goals/{goalId:guid}/contributions")]
[Authorize]
public class GoalContributionsController(IGoalContributionsService contributionsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid goalId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var items = await contributionsService.ListAsync(userId, goalId, cancellationToken);
        if (items is null) return NotFound();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid goalId, [FromBody] GoalContributionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var contrib = await contributionsService.CreateAsync(userId, goalId, request, cancellationToken);
            if (contrib is null) return NotFound();
            return Ok(contrib);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
