using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupsService _lookupsService;
    public LookupsController(ILookupsService lookupsService) => _lookupsService = lookupsService;

    [HttpGet("payment-methods")]
    // Lista formas de pagamento disponíveis (lookup).
    public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
    {
        var data = await _lookupsService.GetPaymentMethodsAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("card-brands")]
    // Lista bandeiras de cartão ativas (lookup).
    public async Task<IActionResult> GetCardBrands(CancellationToken cancellationToken)
    {
        var data = await _lookupsService.GetCardBrandsAsync(cancellationToken);
        return Ok(data);
    }
}
