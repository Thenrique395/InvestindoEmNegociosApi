namespace InvestindoEmNegocio.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresAt <= nowUtc;

    public bool IsRevoked => RevokedAt.HasValue;

    public void Revoke(DateTime nowUtc, string? replacedByTokenHash = null)
    {
        RevokedAt = nowUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
