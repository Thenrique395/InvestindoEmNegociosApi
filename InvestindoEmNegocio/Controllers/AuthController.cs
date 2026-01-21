using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService authService, IAuditService auditService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    // Cria um novo usuário e retorna o token de cadastro (sem realizar login automático no front).
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authService.RegisterAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Register), new { id = response.UserId }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateProblem("Registro inválido", ex.Message, StatusCodes.Status400BadRequest));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(CreateProblem("Registro inválido", ex.Message, StatusCodes.Status409Conflict));
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            await authService.ChangePasswordAsync(userId, request, cancellationToken);
            await auditService.LogAsync(userId, "CHANGE_PASSWORD", "User", userId.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(CreateProblem("Senha inválida", ex.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authService.RefreshAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(CreateProblem("Token inválido", ex.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
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
            var response = await authService.LoginAsync(request, cancellationToken);
            await auditService.LogAsync(response.UserId, "LOGIN", "User", response.UserId.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, CreateProblem("Conta bloqueada", ex.Message, StatusCodes.Status423Locked));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateProblem("Login inválido", ex.Message, StatusCodes.Status400BadRequest));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(CreateProblem("Credenciais inválidas", "E-mail ou senha incorretos.", StatusCodes.Status401Unauthorized));
        }
    }

    private static ProblemDetails CreateProblem(string title, string detail, int statusCode)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode
        };
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
