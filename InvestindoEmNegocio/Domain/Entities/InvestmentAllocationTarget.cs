namespace InvestindoEmNegocio.Domain.Entities;

public class InvestmentAllocationTarget
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public decimal Rf { get; private set; }
    public decimal Acoes { get; private set; }
    public decimal Fundos { get; private set; }
    public decimal Cripto { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private InvestmentAllocationTarget() { }

    public InvestmentAllocationTarget(Guid userId, decimal rf, decimal acoes, decimal fundos, decimal cripto)
    {
        UserId = userId;
        SetAllocation(rf, acoes, fundos, cripto);
    }

    public void SetAllocation(decimal rf, decimal acoes, decimal fundos, decimal cripto)
    {
        Rf = NormalizePercent(rf);
        Acoes = NormalizePercent(acoes);
        Fundos = NormalizePercent(fundos);
        Cripto = NormalizePercent(cripto);
        UpdatedAt = DateTime.UtcNow;
    }

    private static decimal NormalizePercent(decimal value)
    {
        return value switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => decimal.Round(value, 2, MidpointRounding.AwayFromZero)
        };
    }
}
