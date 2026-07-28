namespace InvestindoEmNegocio.Domain.Entities;

// Token de confirmação de e-mail (double opt-in). Espelha PasswordResetToken: guarda o HASH do
// token (nunca o valor bruto), com validade e marcação de uso único.
public class EmailConfirmationToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; private set; }

    private EmailConfirmationToken()
    {
        TokenHash = string.Empty;
    }

    public EmailConfirmationToken(Guid userId, string tokenHash, DateTime expiresAt)
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
