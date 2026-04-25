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
public class LookupsController(
    ILookupPaymentMethodService lookupPaymentMethodService,
    ILookupCardBrandService lookupCardBrandService,
    ILookupInstitutionService lookupInstitutionService) : ControllerBase
{
    [HttpGet("payment-methods")]
    // Lists available payment methods.
    public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
    {
        var data = await lookupPaymentMethodService.GetPaymentMethodsAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("card-brands")]
    // Lists active card brands.
    public async Task<IActionResult> GetCardBrands(CancellationToken cancellationToken)
    {
        var data = await lookupCardBrandService.GetCardBrandsAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("institutions")]
    // Lists active institutions filtered by type when provided.
    public async Task<IActionResult> GetInstitutions([FromQuery] string? type, CancellationToken cancellationToken)
    {
        InstitutionType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<InstitutionType>(type, true, out var typeValue))
            parsedType = typeValue;

        var data = await lookupInstitutionService.GetInstitutionsAsync(parsedType, cancellationToken);
        var response = data.Select(i => new InstitutionLookupResponse(i.Id, i.Name, i.Type.ToString())).ToList();
        return Ok(response);
    }
}
