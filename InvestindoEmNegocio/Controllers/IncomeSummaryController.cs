using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/incomes")]
[Route("api/v1/incomes")]
public class IncomeSummaryController(IIncomeSummaryService incomeSummaryService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureIncomesRead)]
    public async Task<ActionResult<IncomeListResponse>> List([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await incomeSummaryService.GetListAsync(userId, month, cancellationToken);
        return Ok(response);
    }

    [HttpGet("summary")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureIncomesSummaryRead)]
    public async Task<ActionResult<IncomeSummaryResponse>> Summary([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await incomeSummaryService.GetSummaryAsync(userId, month, cancellationToken);
        return Ok(response);
    }
}
