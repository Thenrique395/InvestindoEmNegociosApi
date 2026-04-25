using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/preferences")]
[Route("api/v1/preferences")]
[Authorize(Policy = AppAuthorizationPolicies.FeaturePreferencesManage)]
public class PreferenceSummariesController(IUserPrivacyCenterService userPrivacyCenterService) : AuthenticatedControllerBase
{
    [HttpGet("privacy-summary")]
    public async Task<IActionResult> GetPrivacy(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var summary = await userPrivacyCenterService.GetPrivacySummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("security-summary")]
    public async Task<IActionResult> GetSecurity(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var summary = await userPrivacyCenterService.GetSecuritySummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }
}
