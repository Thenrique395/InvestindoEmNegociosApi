using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class InvestmentPosition : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public InvestmentType Type { get; private set; }
    public string Asset { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal AvgPrice { get; private set; }
    public DateOnly OpenedAt { get; private set; }
    public string Account { get; private set; } = string.Empty;
    public int? InstitutionId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    public List<InvestmentMovement> Movements { get; private set; } = new();

    private InvestmentPosition() { }

    public InvestmentPosition(
        Guid userId,
        Guid spaceId,
        InvestmentType type,
        string asset,
        decimal quantity,
        decimal avgPrice,
        DateOnly openedAt,
        string account,
        string category,
        string? note = null,
        string currency = "BRL")
    {
        UserId = userId;
        SpaceId = spaceId;
        Update(type, asset, quantity, avgPrice, openedAt, account, category, note, currency);
    }

    public void Update(
        InvestmentType type,
        string asset,
        decimal quantity,
        decimal avgPrice,
        DateOnly openedAt,
        string account,
        string category,
        string? note,
        string currency = "BRL")
    {
        Type = type;
        Asset = asset.Trim();
        Quantity = quantity;
        AvgPrice = avgPrice;
        OpenedAt = openedAt;
        Account = account?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? string.Empty;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Currency = NormalizeCurrency(currency);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();

    public void ApplyMovement(InvestmentMovement movement)
    {
        Movements.Insert(0, movement);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetInstitution(int? institutionId)
    {
        InstitutionId = institutionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
