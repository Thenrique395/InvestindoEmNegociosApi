using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class AccountTransaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public AccountTransactionKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AccountTransaction() { }

    public AccountTransaction(
        Guid accountId,
        Guid userId,
        DateTime occurredAt,
        AccountTransactionKind kind,
        decimal amount,
        string description,
        string? sourceType = null,
        Guid? sourceId = null)
    {
        if (amount <= 0) throw new ArgumentException("Valor da movimentação deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Descrição da movimentação é obrigatória.");

        AccountId = accountId;
        UserId = userId;
        OccurredAt = occurredAt.Kind == DateTimeKind.Utc ? occurredAt : DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc);
        Kind = kind;
        Amount = amount;
        Description = description.Trim();
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim();
        SourceId = sourceId;
    }
}
