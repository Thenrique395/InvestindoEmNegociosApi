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
public class AuthEmailConfirmationController(
    IEmailConfirmationApplicationService emailConfirmationApplicationService) : ControllerBase
{
    // Confirma o e-mail a partir do token do link. Público (o usuário ainda não loga).
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> Confirm([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await emailConfirmationApplicationService.ConfirmAsync(request.Token, cancellationToken);
        return Ok(new { confirmed = true });
    }

    // Reenvia o e-mail de confirmação. Resposta sempre 202 (não revela se o e-mail existe).
    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<IActionResult> Resend([FromBody] ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        await emailConfirmationApplicationService.ResendAsync(request.Email, cancellationToken);
        return Accepted();
    }
}
