using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
public class PreferencesController(IPreferencesService preferencesService) : ControllerBase
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

    [HttpGet("privacy-summary")]
    public async Task<IActionResult> GetPrivacySummary(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var summary = await preferencesService.GetPrivacySummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("security-summary")]
    public async Task<IActionResult> GetSecuritySummary(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var summary = await preferencesService.GetSecuritySummaryAsync(userId, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("sessions/revoke")]
    public async Task<IActionResult> RevokeOwnSessions(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var response = await preferencesService.RevokeOwnSessionsAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("account/delete")]
    public async Task<IActionResult> DeleteOwnAccount([FromBody] DeleteOwnAccountRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await preferencesService.DeleteOwnAccountAsync(userId, request, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
