using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
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
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(CreateProblem("Senha inválida", ex.Message, StatusCodes.Status401Unauthorized));
        }
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
            return Ok(response);
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
}
