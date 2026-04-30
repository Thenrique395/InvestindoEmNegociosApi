using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthPasswordApplicationService(
    IAuthPasswordService authPasswordService,
    IAuditService auditService,
    ILogger<AuthPasswordApplicationService> logger) : IAuthPasswordApplicationService
{
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authPasswordService.ForgotPasswordAsync(request, cancellationToken);
            await auditService.LogAsync(null, "FORGOT_PASSWORD_REQUEST", "User", request.Email.Trim().ToLowerInvariant(), ipAddress, userAgent, null, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação em forgot-password para {Email}", request.Email);
            throw new AppProblemException("Requisição inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authPasswordService.ResetPasswordAsync(request, cancellationToken);
            await auditService.LogAsync(null, "RESET_PASSWORD", "User", "password-reset", ipAddress, userAgent, null, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Invalid token na redefinição de senha");
            throw new AppProblemException("Token inválido", ex.Message, StatusCodes.Status401Unauthorized);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação em reset-password");
            throw new AppProblemException("Requisição inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authPasswordService.ChangePasswordAsync(userId, request, cancellationToken);
            await auditService.LogAsync(userId, "CHANGE_PASSWORD", "User", userId.ToString(), ipAddress, userAgent, null, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Tentativa de troca de senha sem autorização para {UserId}", userId);
            throw new AppProblemException("Senha inválida", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }
}
