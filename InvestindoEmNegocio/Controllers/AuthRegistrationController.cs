using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/auth/register")]
[Route("api/v1/auth/register")]
[EnableRateLimiting("auth")]
public class AuthRegistrationController(
    IAuthRegistrationApplicationService authRegistrationApplicationService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<RegisteredUserResponse>> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        // Double opt-in: NÃO loga (sem cookies). O usuário recebe um e-mail e só entra após confirmar.
        var response = await authRegistrationApplicationService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Register), new { id = response.UserId }, response);
    }
}
