using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthFacadeService(
    IAuthService authService,
    IAuditService auditService,
    ILogger<AuthFacadeService> logger) : IAuthFacadeService
{
    public async Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await authService.RegisterAsync(request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação ao registrar usuário para {Email}", request.Email);
            throw new AppProblemException("Registro inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Conflito de registro para {Email}", request.Email);
            throw new AppProblemException("Registro inválido", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            await auditService.LogAsync(response.UserId, "LOGIN", "User", response.UserId.ToString(), ipAddress, userAgent, null, cancellationToken);
            return response;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Conta bloqueada para {Email}", request.Email);
            throw new AppProblemException("Conta bloqueada", ex.Message, StatusCodes.Status423Locked);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação no login para {Email}", request.Email);
            throw new AppProblemException("Login inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Credenciais inválidas para {Email}", request.Email);
            throw new AppProblemException("Credenciais inválidas", "E-mail ou senha incorretos.", StatusCodes.Status401Unauthorized);
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authService.ChangePasswordAsync(userId, request, cancellationToken);
            await auditService.LogAsync(userId, "CHANGE_PASSWORD", "User", userId.ToString(), ipAddress, userAgent, null, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Tentativa de troca de senha sem autorização para {UserId}", userId);
            throw new AppProblemException("Senha inválida", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authService.ForgotPasswordAsync(request, cancellationToken);
            await auditService.LogAsync(null, "FORGOT_PASSWORD_REQUEST", "User", request.Email.Trim().ToLowerInvariant(), ipAddress, userAgent, null, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação em forgot-password para {Email}", request.Email);
            throw new AppProblemException("Solicitação inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await authService.ResetPasswordAsync(request, cancellationToken);
            await auditService.LogAsync(null, "RESET_PASSWORD", "User", "password-reset", ipAddress, userAgent, null, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Token inválido na redefinição de senha");
            throw new AppProblemException("Token inválido", ex.Message, StatusCodes.Status401Unauthorized);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação em reset-password");
            throw new AppProblemException("Solicitação inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await authService.RefreshAsync(request, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Refresh token inválido");
            throw new AppProblemException("Token inválido", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        authService.LogoutAsync(request, cancellationToken);
}
