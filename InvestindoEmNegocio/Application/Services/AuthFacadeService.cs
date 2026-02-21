using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthFacadeService(
    IAuthService authService,
    IAuditService auditService) : IAuthFacadeService
{
    public async Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await authService.RegisterAsync(request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Registro inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
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
            throw new AppProblemException("Conta bloqueada", ex.Message, StatusCodes.Status423Locked);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Login inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
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
            throw new AppProblemException("Senha inválida", ex.Message, StatusCodes.Status401Unauthorized);
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
            throw new AppProblemException("Token inválido", ex.Message, StatusCodes.Status401Unauthorized);
        }
    }

    public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        authService.LogoutAsync(request, cancellationToken);
}
