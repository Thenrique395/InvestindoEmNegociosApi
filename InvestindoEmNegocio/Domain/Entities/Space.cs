using InvestindoEmNegocio.Domain.Common;

namespace InvestindoEmNegocio.Domain.Entities;

public class Space : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    private Space() { }

    public Space(Guid userId, string name, bool isDefault = false, string? passwordHash = null)
    {
        UserId = userId;
        Rename(name);
        IsDefault = isDefault;
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash;
    }

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome do espaço é obrigatório.");
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string? passwordHash)
    {
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
