using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/investments/market")]
[Route("api/v1/investments/market")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvestmentsAccess)]
public class InvestmentMarketController(IInvestmentsApplicationService investmentsApplicationService) : ControllerBase
{
    [HttpGet("quote")]
    public async Task<ActionResult<MarketQuoteResponse>> GetQuote([FromQuery] string symbol, CancellationToken cancellationToken = default)
    {
        var response = await investmentsApplicationService.GetMarketQuoteAsync(symbol, cancellationToken);
        return Ok(response);
    }

    [HttpGet("profile")]
    public async Task<ActionResult<MarketProfileResponse>> GetProfile([FromQuery] string symbol, CancellationToken cancellationToken = default)
    {
        var response = await investmentsApplicationService.GetMarketProfileAsync(symbol, cancellationToken);
        return Ok(response);
    }

    [HttpGet("history")]
    public async Task<ActionResult<MarketHistoryResponse>> GetHistory([FromQuery] string symbol, [FromQuery] string period = "6mo", CancellationToken cancellationToken = default)
    {
        var response = await investmentsApplicationService.GetMarketHistoryAsync(symbol, period, cancellationToken);
        return Ok(response);
    }
}
