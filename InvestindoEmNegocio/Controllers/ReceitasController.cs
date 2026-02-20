using System.Globalization;
using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/receitas")]
[Route("api/v1/receitas")]
[Authorize]
public class ReceitasController : ControllerBase
{
    private readonly IIncomeSummaryUseCase _incomeSummaryUseCase;

    public ReceitasController(IIncomeSummaryUseCase incomeSummaryUseCase)
    {
        _incomeSummaryUseCase = incomeSummaryUseCase;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IncomeSummaryResponse>> Summary([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await _incomeSummaryUseCase.ExecuteAsync(userId, month, cancellationToken);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}