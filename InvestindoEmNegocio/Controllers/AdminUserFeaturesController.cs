using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/users/{id:guid}/features")]
[Route("api/v1/admin/users/{id:guid}/features")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminUsersManage)]
public class AdminUserFeaturesController(IAdminUsersService adminUsersService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        var response = await adminUsersService.ListFeaturesAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{featureKey}")]
    public async Task<IActionResult> SetOverride(
        Guid id,
        string featureKey,
        [FromBody] SetUserFeatureOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var response = await adminUsersService.SetFeatureOverrideAsync(
            id,
            featureKey,
            request.IsEnabled,
            GetUserId(),
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{featureKey}")]
    public async Task<IActionResult> ClearOverride(Guid id, string featureKey, CancellationToken cancellationToken)
    {
        var response = await adminUsersService.ClearFeatureOverrideAsync(
            id,
            featureKey,
            GetUserId(),
            GetIpAddress(),
            GetUserAgent(),
            cancellationToken);
        return Ok(response);
    }
}
