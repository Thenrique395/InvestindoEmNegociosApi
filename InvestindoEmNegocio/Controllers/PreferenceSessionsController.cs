using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/preferences/sessions")]
[Route("api/v1/preferences/sessions")]
[Authorize(Policy = AppAuthorizationPolicies.FeaturePreferencesManage)]
public class PreferenceSessionsController(IUserPrivacyCenterService userPrivacyCenterService) : AuthenticatedControllerBase
{
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeOwn(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var response = await userPrivacyCenterService.RevokeOwnSessionsAsync(userId, cancellationToken);
        return Ok(response);
    }
}
