using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthService(
    IUserRepository userRepository,
    IUserAccountBootstrapService userAccountBootstrapService,
    IUserSessionService userSessionService,
    IPasswordResetService passwordResetService,
    ILogger<AuthService> logger)
    : IAuthService
{
    private readonly ILogger<AuthService> _logger = logger;
    private const int BcryptWorkFactor = 12;
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException("E-mail já está em uso.");
        }

        var passwordHash = BCryptNet.HashPassword(request.Password, BcryptWorkFactor);
        var user = new User(request.Name.Trim(), request.Email.Trim().ToLowerInvariant(), passwordHash);

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userAccountBootstrapService.EnsureDefaultAccountForBasicAsync(user, cancellationToken);

        _logger.LogInformation("User registered {UserId}", user.Id);
        return await userSessionService.IssueAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        var now = DateTime.UtcNow;

        if (user is null)
        {
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        if (user.IsLocked(now))
        {
            _logger.LogWarning("Login blocked due to lockout {UserId}", user.Id);
            throw new InvalidOperationException("Conta bloqueada temporariamente. Tente novamente mais tarde.");
        }

        if (!BCryptNet.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now, MaxFailedLoginAttempts, LockoutDuration);
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
        {
            user.ResetFailedLogins(now);
        }

        // Backfill safety for migrated Basic users created before account-by-transaction model.
        await userAccountBootstrapService.EnsureDefaultAccountForBasicAsync(user, cancellationToken);

        user.UpdateLastLogin(now);
        // Como o UpdateLastLogin já atualiza UpdatedAt, apenas persistimos
        await userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in {UserId}", user.Id);
        return await userSessionService.IssueAsync(user, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new UnauthorizedAccessException("Usuário não encontrado.");

        if (!BCryptNet.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Senha atual inválida.");

        var newHash = BCryptNet.HashPassword(request.NewPassword, BcryptWorkFactor);
        user.ChangePassword(newHash);
        await userRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password changed {UserId}", user.Id);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await passwordResetService.ForgotPasswordAsync(request, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await passwordResetService.ResetPasswordAsync(request, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var stored = await userSessionService.GetActiveByRawTokenAsync(request.RefreshToken, now, cancellationToken);

        if (stored is null || stored.IsRevoked || stored.IsExpired(now))
        {
            throw new UnauthorizedAccessException("Refresh token inválido.");
        }

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Usuário não encontrado.");
        }

        _logger.LogInformation("Refresh token rotated {UserId}", user.Id);
        return await userSessionService.RotateAsync(user, stored, now, cancellationToken);
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await userSessionService.RevokeByRawTokenAsync(request.RefreshToken, cancellationToken);
    }
}
