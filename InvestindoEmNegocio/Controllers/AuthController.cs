using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthFacadeService authFacadeService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    // Cria um novo usuário e retorna o token de cadastro (sem realizar login automático no front).
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authFacadeService.RegisterAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Register), new { id = response.UserId }, response);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            await authFacadeService.ChangePasswordAsync(userId, request, GetIpAddress(), GetUserAgent(), cancellationToken);
            return NoContent();
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authFacadeService.RefreshAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authFacadeService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    // Autentica um usuário existente e devolve token JWT para as próximas requisições.
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authFacadeService.LoginAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
            return Ok(response);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
