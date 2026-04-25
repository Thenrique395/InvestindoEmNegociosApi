using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/card-brands")]
[Route("api/v1/admin/parameters/card-brands")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminCardBrandsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListCardBrandsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCardBrandRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreateCardBrandAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateCardBrandStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }
}
