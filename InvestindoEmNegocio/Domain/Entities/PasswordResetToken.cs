namespace InvestindoEmNegocio.Domain.Entities;

public class PasswordResetToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; private set; }

    private PasswordResetToken()
    {
        TokenHash = string.Empty;
    }

    public PasswordResetToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresAt <= nowUtc;
    public bool IsUsed => UsedAt.HasValue;

    public void MarkAsUsed(DateTime nowUtc)
    {
        UsedAt = nowUtc;
    }
}
