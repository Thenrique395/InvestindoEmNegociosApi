using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthAccessApplicationService(
    IAuthService authService,
    IAuditService auditService,
    ILogger<AuthAccessApplicationService> logger) : IAuthAccessApplicationService
{
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
            logger.LogWarning(ex, "Account locked para {Email}", request.Email);
            throw new AppProblemException("Conta bloqueada", ex.Message, StatusCodes.Status423Locked);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação no login para {Email}", request.Email);
            throw new AppProblemException("Login inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogWarning("Invalid credentials para {Email}", request.Email);
            throw new AppProblemException("Credenciais inválidas", "Email ou senha incorretos.", StatusCodes.Status401Unauthorized);
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
