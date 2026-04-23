using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/lookups")]
[Route("api/v1/lookups")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureLookupsRead)]
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

    [HttpGet("institutions")]
    // Lista bancos/corretoras ativos (lookup).
    public async Task<IActionResult> GetInstitutions([FromQuery] string? type, CancellationToken cancellationToken)
    {
        InstitutionType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<InstitutionType>(type, true, out var typeValue))
            parsedType = typeValue;

        var data = await _lookupsService.GetInstitutionsAsync(parsedType, cancellationToken);
        var response = data.Select(i => new InstitutionLookupResponse(i.Id, i.Name, i.Type.ToString())).ToList();
        return Ok(response);
    }
}
