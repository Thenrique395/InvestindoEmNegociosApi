using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Account
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public decimal InitialBalance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private Account() { }

    public Account(Guid userId, string name, AccountType type, decimal initialBalance = 0m)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome da conta é obrigatório.");

        UserId = userId;
        Name = name.Trim();
        Type = type;
        InitialBalance = initialBalance;
    }

    public void Update(string name, AccountType type, decimal initialBalance)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome da conta é obrigatório.");

        Name = name.Trim();
        Type = type;
        InitialBalance = initialBalance;
        UpdatedAt = DateTime.UtcNow;
    }

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
}
