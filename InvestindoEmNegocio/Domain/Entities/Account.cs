using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Account : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public decimal InitialBalance { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    private Account() { }

    public Account(Guid userId, Guid spaceId, string name, AccountType type, decimal initialBalance = 0m, string currency = "BRL")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome da conta é obrigatório.");

        UserId = userId;
        SpaceId = spaceId;
        Name = name.Trim();
        Type = type;
        InitialBalance = initialBalance;
        Currency = NormalizeCurrency(currency);
    }

    public void Update(string name, AccountType type, decimal initialBalance, string currency = "BRL")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome da conta é obrigatório.");

        Name = name.Trim();
        Type = type;
        InitialBalance = initialBalance;
        Currency = NormalizeCurrency(currency);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void RestoreCreatedAt(DateTime createdAt)
    {
        CreatedAt = createdAt.Kind == DateTimeKind.Utc
            ? createdAt
            : DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
