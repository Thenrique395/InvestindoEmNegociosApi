namespace InvestindoEmNegocio.Domain.Entities;

public class CardBrand
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private CardBrand() { }

    public CardBrand(int id, string name, string code, bool isActive = true)
    {
        Id = id;
        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
