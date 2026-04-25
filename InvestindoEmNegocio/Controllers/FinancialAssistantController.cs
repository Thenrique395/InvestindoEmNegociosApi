using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/financial-assistant")]
[Route("api/v1/financial-assistant")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureFinancialAssistantAccess)]
public class FinancialAssistantController(IFinancialAssistantService financialAssistantService) : AuthenticatedControllerBase
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

}
