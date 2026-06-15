using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
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
    IAuthAvailabilityService authAvailabilityService) : AuthenticatedControllerBase
{
    [HttpPost("check-availability")]
    [AllowAnonymous]
    public async Task<ActionResult<CheckAvailabilityResponse>> CheckAvailability([FromBody] CheckAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await authAvailabilityService.CheckAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authAccessApplicationService.LoginAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await authAccessApplicationService.RefreshAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authAccessApplicationService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
