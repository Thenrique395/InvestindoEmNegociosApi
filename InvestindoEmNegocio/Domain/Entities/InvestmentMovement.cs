using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class InvestmentMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PositionId { get; private set; }
    public InvestmentMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public DateOnly Date { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public InvestmentPosition? Position { get; private set; }

    private InvestmentMovement() { }

    public InvestmentMovement(Guid positionId, InvestmentMovementType type, decimal quantity, decimal price, DateOnly date, string? note = null)
    {
        PositionId = positionId;
        Type = type;
        Quantity = quantity;
        Price = price;
        Date = date;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
