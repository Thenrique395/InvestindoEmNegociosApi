using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/robot-settings")]
[Route("api/v1/admin/parameters/robot-settings")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminRobotSettingsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.GetRobotSettingsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateRobotSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateRobotSettingsAsync(request, cancellationToken);
        return Ok(response);
    }
}
