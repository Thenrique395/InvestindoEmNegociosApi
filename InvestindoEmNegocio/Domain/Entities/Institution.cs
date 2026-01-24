using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Institution
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public InstitutionType Type { get; private set; } = InstitutionType.Bank;
    public bool IsActive { get; private set; } = true;

    private Institution() { }

    public Institution(string name, InstitutionType type, bool isActive = true)
    {
        Name = name.Trim().ToUpperInvariant();
        Type = type;
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
