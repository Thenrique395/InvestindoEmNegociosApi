using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/users")]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        var response = users
            .Select(u => new UserSummaryResponse(u.Id, u.Name, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToList();
        return Ok(response);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Role inválida",
                Detail = "Role informada não é válida.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.SetRole(role);
        await userRepository.SaveChangesAsync(cancellationToken);

        var response = new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
        return Ok(response);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        if (id == currentUserId && !request.IsActive)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Ação inválida",
                Detail = "Você não pode bloquear seu próprio acesso.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        var response = new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        if (id == currentUserId)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Ação inválida",
                Detail = "Você não pode excluir seu próprio usuário.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        userRepository.Remove(user);
        await userRepository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
