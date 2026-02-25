using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/users")]
[Route("api/v1/admin/users")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminUsersManage)]
public class AdminUsersController(IAdminUsersService adminUsersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await adminUsersService.ListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await adminUsersService.UpdateRoleAsync(id, request.Role, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        var response = await adminUsersService.UpdateStatusAsync(id, request.IsActive, currentUserId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}/features")]
    public async Task<IActionResult> ListFeatures(Guid id, CancellationToken cancellationToken)
    {
        var response = await adminUsersService.ListFeaturesAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/features/{featureKey}")]
    public async Task<IActionResult> SetFeatureOverride(
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

    [HttpDelete("{id:guid}/features/{featureKey}")]
    public async Task<IActionResult> ClearFeatureOverride(Guid id, string featureKey, CancellationToken cancellationToken)
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        await adminUsersService.DeleteAsync(id, currentUserId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
