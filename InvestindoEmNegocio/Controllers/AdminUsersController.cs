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
public class AdminUsersController(IAdminUsersService adminUsersService) : AuthenticatedControllerBase
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        await adminUsersService.DeleteAsync(id, currentUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/trial")]
    public async Task<IActionResult> GrantTrial(Guid id, [FromBody] GrantTrialRequest request, CancellationToken cancellationToken)
    {
        var response = await adminUsersService.GrantTrialAsync(id, request, cancellationToken);
        return Ok(response);
    }

}
