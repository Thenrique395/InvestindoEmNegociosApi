using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/reports")]
[Route("api/v1/reports")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureReportsAccess)]
public class ReportsController(IReportService reportService) : AuthenticatedControllerBase
{
    [HttpGet("monthly-summary/{year:int}/{month:int}")]
    public async Task<IActionResult> MonthlySummary(int year, int month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await reportService.GetMonthlySummaryAsync(userId, year, month, cancellationToken));
    }
}
