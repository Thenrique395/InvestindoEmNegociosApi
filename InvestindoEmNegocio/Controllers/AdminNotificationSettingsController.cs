using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/notification-settings")]
[Route("api/v1/admin/parameters/notification-settings")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminNotificationSettingsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.GetNotificationSettingsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateNotificationSettingsAsync(request, cancellationToken);
        return Ok(response);
    }
}
