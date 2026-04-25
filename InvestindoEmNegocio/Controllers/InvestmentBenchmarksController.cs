using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/investments/benchmarks")]
[Route("api/v1/investments/benchmarks")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvestmentsAccess)]
public class InvestmentBenchmarksController(IInvestmentBenchmarksService benchmarksService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvestmentBenchmarksResponse>> Get([FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        var response = await benchmarksService.GetBenchmarksAsync(months, cancellationToken);
        return Ok(response);
    }
}
