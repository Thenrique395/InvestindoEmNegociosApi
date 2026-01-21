using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class InvestmentPosition
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public InvestmentType Type { get; private set; }
    public string Asset { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal AvgPrice { get; private set; }
    public DateOnly OpenedAt { get; private set; }
    public string Account { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public List<InvestmentMovement> Movements { get; private set; } = new();

    private InvestmentPosition() { }

    public InvestmentPosition(
        Guid userId,
        InvestmentType type,
        string asset,
        decimal quantity,
        decimal avgPrice,
        DateOnly openedAt,
        string account,
        string category,
        string? note = null)
    {
        UserId = userId;
        Update(type, asset, quantity, avgPrice, openedAt, account, category, note);
    }

    public void Update(
        InvestmentType type,
        string asset,
        decimal quantity,
        decimal avgPrice,
        DateOnly openedAt,
        string account,
        string category,
        string? note)
    {
        Type = type;
        Asset = asset.Trim();
        Quantity = quantity;
        AvgPrice = avgPrice;
        OpenedAt = openedAt;
        Account = account?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? string.Empty;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyMovement(InvestmentMovement movement)
    {
        Movements.Insert(0, movement);
        UpdatedAt = DateTime.UtcNow;
    }
}
