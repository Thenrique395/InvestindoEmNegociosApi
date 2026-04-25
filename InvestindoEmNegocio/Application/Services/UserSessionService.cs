using System.Security.Cryptography;
using System.Text;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class UserSessionService(
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenGenerator jwtTokenGenerator) : IUserSessionService
{
    public async Task<AuthResponse> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var access = jwtTokenGenerator.Generate(user);
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashedRefreshToken = HashToken(rawRefreshToken);
        var refreshEntity = new RefreshToken(user.Id, hashedRefreshToken, DateTime.UtcNow.AddDays(30));
        await refreshTokenRepository.AddAsync(refreshEntity, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Name, user.Email, user.Role.ToString(), access.Token, rawRefreshToken, access.ExpiresAt);
    }

    public async Task<AuthResponse> ReissueAsync(User user, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await RevokeActiveAsync(user.Id, nowUtc, cancellationToken);
        return await IssueAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RotateAsync(User user, RefreshToken currentToken, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var issued = await IssueAsync(user, cancellationToken);
        currentToken.Revoke(nowUtc, HashToken(issued.RefreshToken));
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<RefreshToken?> GetActiveByRawTokenAsync(string rawRefreshToken, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawRefreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (stored is null || stored.IsRevoked || stored.IsExpired(nowUtc))
            return null;

        return stored;
    }

    public async Task RevokeByRawTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var stored = await GetActiveByRawTokenAsync(rawRefreshToken, now, cancellationToken);
        if (stored is null)
            return;

        stored.Revoke(now);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await refreshTokenRepository.RevokeActiveByUserAsync(userId, nowUtc, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
