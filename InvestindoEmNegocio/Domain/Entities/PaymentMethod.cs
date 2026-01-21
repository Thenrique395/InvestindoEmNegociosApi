namespace InvestindoEmNegocio.Domain.Entities;

public class PaymentMethod
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private PaymentMethod() { }

    public PaymentMethod(int id, string name)
    {
        Id = id;
        Name = name.Trim();
    }
}
