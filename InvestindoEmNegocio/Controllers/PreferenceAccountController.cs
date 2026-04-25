using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/preferences/account")]
[Route("api/v1/preferences/account")]
[Authorize(Policy = AppAuthorizationPolicies.FeaturePreferencesManage)]
public class PreferenceAccountController(IUserPrivacyCenterService userPrivacyCenterService) : AuthenticatedControllerBase
{
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteOwn([FromBody] DeleteOwnAccountRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await userPrivacyCenterService.DeleteOwnAccountAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
