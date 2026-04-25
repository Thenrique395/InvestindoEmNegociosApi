namespace InvestindoEmNegocio.Domain.Entities;

public class Expense
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime DueAt { get; private set; }
    public Guid? CardId { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public int? InstallmentsTotal { get; private set; }
    public string? SeriesId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Expense() { }

    public Expense(Guid userId, string name, string category, decimal amount, DateTime dueAt, Guid? cardId = null,
        int? installmentNumber = null, int? installmentsTotal = null, string? seriesId = null)
    {
        UserId = userId;
        Name = name.Trim();
        Category = category.Trim();
        Amount = amount;
        DueAt = dueAt.Kind == DateTimeKind.Utc ? dueAt : DateTime.SpecifyKind(dueAt, DateTimeKind.Utc);
        CardId = cardId;
        InstallmentNumber = installmentNumber;
        InstallmentsTotal = installmentsTotal;
        SeriesId = seriesId;
    }
}
