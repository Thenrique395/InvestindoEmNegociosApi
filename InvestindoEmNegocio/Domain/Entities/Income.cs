namespace InvestindoEmNegocio.Domain.Entities;

public class Income
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public bool IsRecurring { get; private set; }
    public string? RecurringStart { get; private set; }
    public string? RecurringEnd { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Income() { }

    public Income(Guid userId, string source, decimal amount, DateTime receivedAt, bool isRecurring = false, string? recurringStart = null, string? recurringEnd = null)
    {
        UserId = userId;
        Source = source.Trim();
        Amount = amount;
        ReceivedAt = receivedAt.Kind == DateTimeKind.Utc ? receivedAt : DateTime.SpecifyKind(receivedAt, DateTimeKind.Utc);
        IsRecurring = isRecurring;
        RecurringStart = recurringStart;
        RecurringEnd = recurringEnd;
    }
}
