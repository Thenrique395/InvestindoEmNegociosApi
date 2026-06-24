using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/auth")]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    IAuthAccessApplicationService authAccessApplicationService,
    IAuthAvailabilityService authAvailabilityService,
    IAuthCookieService authCookieService) : AuthenticatedControllerBase
{
    private const int RefreshTokenDays = 30;

    [HttpPost("check-availability")]
    [AllowAnonymous]
    public async Task<ActionResult<CheckAvailabilityResponse>> CheckAvailability([FromBody] CheckAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await authAvailabilityService.CheckAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSessionResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authAccessApplicationService.LoginAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
        SetSessionCookies(response);
        return Ok(ToSessionResponse(response));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthCookieService.RefreshTokenCookie];
        if (string.IsNullOrEmpty(refreshToken))
            throw new AppProblemException("Token inválido", "Sessão não encontrada.", StatusCodes.Status401Unauthorized);

        var response = await authAccessApplicationService.RefreshAsync(new RefreshTokenRequest(refreshToken), cancellationToken);
        SetSessionCookies(response);
        return Ok(ToSessionResponse(response));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthCookieService.RefreshTokenCookie];
        if (!string.IsNullOrEmpty(refreshToken))
            await authAccessApplicationService.LogoutAsync(new RefreshTokenRequest(refreshToken), cancellationToken);

        authCookieService.ClearAuthCookies(Response);
        return NoContent();
    }

    private void SetSessionCookies(AuthResponse response)
    {
        authCookieService.SetAuthCookies(Response, response.Token, response.ExpiresAt, response.RefreshToken, DateTime.UtcNow.AddDays(RefreshTokenDays));
        authCookieService.SetCsrfCookie(Response);
    }

    private static AuthSessionResponse ToSessionResponse(AuthResponse response) =>
        new(response.UserId, response.Name, response.Email, response.Role, response.ExpiresAt);
}
