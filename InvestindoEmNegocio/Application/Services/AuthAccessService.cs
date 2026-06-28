using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthAccessService(
    IUserRepository userRepository,
    IUserAccountBootstrapService userAccountBootstrapService,
    ISpaceBootstrapService spaceBootstrapService,
    IUserSessionService userSessionService,
    ILogger<AuthAccessService> logger) : IAuthAccessService
{
    private readonly ILogger<AuthAccessService> _logger = logger;

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(AuthServicePolicies.NormalizeEmail(request.Email), cancellationToken);
        var now = DateTime.UtcNow;

        if (user is null)
            throw new UnauthorizedAccessException("Credenciais inválidas.");

        if (user.IsLocked(now))
        {
            _logger.LogWarning("Login blocked due to lockout {UserId}", user.Id);
            throw new InvalidOperationException("Conta bloqueada temporariamente. Tente novamente mais tarde.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login blocked, account deactivated {UserId}", user.Id);
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        if (!BCryptNet.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now, AuthServicePolicies.MaxFailedLoginAttempts, AuthServicePolicies.LockoutDuration);
            await userRepository.SaveChangesAsync(cancellationToken);
            if (user.IsLocked(now))
            {
                _logger.LogWarning("Login blocked due to lockout {UserId}", user.Id);
                throw new InvalidOperationException("Conta bloqueada temporariamente. Tente novamente mais tarde.");
            }

            _logger.LogWarning("Invalid login attempt {UserId}", user.Id);
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        if (user.FailedLoginAttempts > 0 || user.LockoutUntil.HasValue)
            user.ResetFailedLogins(now);

        var spaceId = await spaceBootstrapService.EnsureDefaultSpaceAsync(user, cancellationToken);
        await userAccountBootstrapService.EnsureDefaultAccountForBasicAsync(user, spaceId, cancellationToken);
        user.UpdateLastLogin(now);
        await userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in {UserId}", user.Id);
        return await userSessionService.IssueAsync(user, spaceId, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var stored = await userSessionService.GetActiveByRawTokenAsync(request.RefreshToken, now, cancellationToken);

        if (stored is null || stored.IsRevoked || stored.IsExpired(now))
            throw new UnauthorizedAccessException("Refresh token inválido.");

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Usuário não encontrado.");

        if (!user.IsActive)
        {
            _logger.LogWarning("Refresh blocked, account deactivated {UserId}", user.Id);
            throw new UnauthorizedAccessException("Refresh token inválido.");
        }

        _logger.LogInformation("Refresh token rotated {UserId}", user.Id);
        return await userSessionService.RotateAsync(user, stored, now, cancellationToken);
    }

    public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        userSessionService.RevokeByRawTokenAsync(request.RefreshToken, cancellationToken);
}
