namespace InvestindoEmNegocio.Domain.Entities;

public class PaymentMethod
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private PaymentMethod() { }

    public PaymentMethod(int id, string name, bool isActive = true)
    {
        Id = id;
        Name = name.Trim();
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
