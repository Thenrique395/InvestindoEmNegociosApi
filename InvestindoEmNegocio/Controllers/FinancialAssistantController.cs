using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AppAuthorizationPolicies.AtLeastIntermediate)]
public class FinancialAssistantController(IFinancialAssistantService financialAssistantService) : ControllerBase
{
    [HttpGet("context")]
    public async Task<IActionResult> Context([FromQuery] DateOnly? referenceDate, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await financialAssistantService.BuildContextAsync(userId, referenceDate, cancellationToken));
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] FinancialAssistantChatRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await financialAssistantService.ChatAsync(userId, request, cancellationToken));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
