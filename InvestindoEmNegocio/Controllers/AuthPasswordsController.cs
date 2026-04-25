using InvestindoEmNegocio.Application.DTOs;
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
public class AuthPasswordsController(IAuthPasswordApplicationService authPasswordApplicationService) : AuthenticatedControllerBase
{
    [HttpPost("change-password")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAuthSecurityManage)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await authPasswordApplicationService.ChangePasswordAsync(userId, request, GetIpAddress(), GetUserAgent(), cancellationToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authPasswordApplicationService.ForgotPasswordAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await authPasswordApplicationService.ResetPasswordAsync(request, GetIpAddress(), GetUserAgent(), cancellationToken);
        return NoContent();
    }
}
