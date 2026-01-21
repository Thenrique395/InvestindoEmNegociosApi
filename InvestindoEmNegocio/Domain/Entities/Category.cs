using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Category
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MoneyType? AppliesTo { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Category() { }

    public Category(Guid? userId, string name, MoneyType? appliesTo)
    {
        UserId = userId;
        Name = name.Trim();
        AppliesTo = appliesTo;
    }
}
