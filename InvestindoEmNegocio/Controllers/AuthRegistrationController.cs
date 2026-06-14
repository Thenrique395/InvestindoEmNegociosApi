using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/auth/register")]
[Route("api/v1/auth/register")]
[EnableRateLimiting("auth")]
public class AuthRegistrationController(
    IAuthRegistrationApplicationService authRegistrationApplicationService,
    IAuthCookieService authCookieService) : ControllerBase
{
    private const int RefreshTokenDays = 30;

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSessionResponse>> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var response = await authRegistrationApplicationService.RegisterAsync(request, cancellationToken);
        authCookieService.SetAuthCookies(Response, response.Token, response.ExpiresAt, response.RefreshToken, DateTime.UtcNow.AddDays(RefreshTokenDays));
        authCookieService.SetCsrfCookie(Response);
        var session = new AuthSessionResponse(response.UserId, response.Name, response.Email, response.Role, response.ExpiresAt);
        return CreatedAtAction(nameof(Register), new { id = response.UserId }, session);
    }
}
