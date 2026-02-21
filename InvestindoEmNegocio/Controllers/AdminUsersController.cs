using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/users")]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
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
        try
        {
            var response = await adminUsersService.UpdateRoleAsync(id, request.Role, cancellationToken);
            return Ok(response);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = GetUserId();
            var response = await adminUsersService.UpdateStatusAsync(id, request.IsActive, currentUserId, cancellationToken);
            return Ok(response);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = GetUserId();
            await adminUsersService.DeleteAsync(id, currentUserId, cancellationToken);
            return NoContent();
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
