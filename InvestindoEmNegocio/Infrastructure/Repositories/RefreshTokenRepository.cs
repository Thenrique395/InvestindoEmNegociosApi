using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class RefreshTokenRepository(InvestDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        return context.RefreshTokens.AddAsync(token, cancellationToken).AsTask();
    }

    public async Task RevokeActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.RevokedAt.HasValue && rt.ExpiresAt > nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(nowUtc);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
