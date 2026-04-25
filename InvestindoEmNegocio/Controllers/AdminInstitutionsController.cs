using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/institutions")]
[Route("api/v1/admin/parameters/institutions")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminInstitutionsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListInstitutionsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstitutionRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreateInstitutionAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateInstitutionStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }
}
