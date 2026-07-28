using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class EmailConfirmationApplicationService(
    IEmailConfirmationService emailConfirmationService,
    ILogger<EmailConfirmationApplicationService> logger) : IEmailConfirmationApplicationService
{
    public async Task ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            await emailConfirmationService.ConfirmAsync(token, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Token de confirmação inválido");
            throw new AppProblemException("Token inválido", ex.Message, StatusCodes.Status400BadRequest, code: "invalid_confirmation_token");
        }
    }

    public async Task ResendAsync(string email, CancellationToken cancellationToken = default)
    {
        // Silencioso por design (não revela se o e-mail existe / está confirmado).
        await emailConfirmationService.ResendAsync(email, cancellationToken);
        logger.LogInformation("Resend confirmation requested for {Email}", LogMasking.Email(email));
    }
}
