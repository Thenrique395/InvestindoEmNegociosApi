using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/receitas")]
[Route("api/v1/receitas")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureIncomesSummaryRead)]
public class ReceitasController(IIncomeSummaryService incomeSummaryService) : AuthenticatedControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<IncomeSummaryResponse>> Summary([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await incomeSummaryService.GetSummaryAsync(userId, month, cancellationToken);
        return Ok(response);
    }

}
