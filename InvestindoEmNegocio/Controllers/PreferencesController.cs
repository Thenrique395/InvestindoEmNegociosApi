using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/preferences")]
[Route("api/v1/preferences")]
[Authorize(Policy = AppAuthorizationPolicies.FeaturePreferencesManage)]
public class PreferencesController(IPreferenceSettingsService preferenceSettingsService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var prefs = await preferenceSettingsService.GetAsync(userId, cancellationToken);
        return Ok(prefs);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var prefs = await preferenceSettingsService.UpdateAsync(userId, request, cancellationToken);
        return Ok(prefs);
    }

    // Endpoint leve pro toggle de tema (não reenvia currency/locale/notificações).
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var theme = await preferenceSettingsService.UpdateThemeAsync(userId, request.Theme, cancellationToken);
        return Ok(new { theme });
    }
}
