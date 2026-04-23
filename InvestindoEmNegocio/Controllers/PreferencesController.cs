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
public class PreferencesController(IPreferencesService preferencesService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var prefs = await preferencesService.GetAsync(userId, cancellationToken);
        return Ok(prefs);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var prefs = await preferencesService.UpdateAsync(userId, request, cancellationToken);
        return Ok(prefs);
    }

}
