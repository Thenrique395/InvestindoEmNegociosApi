using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using System.Security.Cryptography;
using System.Text;
using InvestindoEmNegocio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    ILogger<AuthService> logger)
    : IAuthService
{
    private readonly ILogger<AuthService> _logger = logger;
    private const int BcryptWorkFactor = 12;
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

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

        _logger.LogInformation("User registered {UserId}", user.Id);

        var access = jwtTokenGenerator.Generate(user);
        var refresh = await IssueRefreshTokenAsync(user, cancellationToken);
        return new AuthResponse(user.Id, user.Name, user.Email, access.Token, refresh.Token, access.ExpiresAt);
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
            _logger.LogWarning("Invalid login attempt {UserId}", user.Id);
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        if (user.FailedLoginAttempts > 0 || user.LockoutUntil.HasValue)
        {
            user.ResetFailedLogins(now);
        }

        user.UpdateLastLogin(now);
        // Como o UpdateLastLogin já atualiza UpdatedAt, apenas persistimos
        await userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in {UserId}", user.Id);

        var access = jwtTokenGenerator.Generate(user);
        var refresh = await IssueRefreshTokenAsync(user, cancellationToken);
        return new AuthResponse(user.Id, user.Name, user.Email, access.Token, refresh.Token, access.ExpiresAt);
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

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(request.RefreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (stored is null || stored.IsRevoked || stored.IsExpired(now))
        {
            throw new UnauthorizedAccessException("Refresh token inválido.");
        }

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Usuário não encontrado.");
        }

        var access = jwtTokenGenerator.Generate(user);
        var refresh = await IssueRefreshTokenAsync(user, cancellationToken);
        stored.Revoke(now, HashToken(refresh.Token));
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated {UserId}", user.Id);

        return new AuthResponse(user.Id, user.Name, user.Email, access.Token, refresh.Token, access.ExpiresAt);
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(request.RefreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (stored is null || stored.IsRevoked || stored.IsExpired(now))
        {
            return;
        }

        stored.Revoke(now);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User logged out {UserId}", stored.UserId);
    }

    private async Task<(string Token, DateTime ExpiresAt)> IssueRefreshTokenAsync(User user, CancellationToken cancellationToken)
    {
        var token = GenerateRefreshToken();
        var tokenHash = HashToken(token);
        var expiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);

        await refreshTokenRepository.AddAsync(new RefreshToken(user.Id, tokenHash, expiresAt), cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return (token, expiresAt);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
