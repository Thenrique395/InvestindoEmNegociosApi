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
        if (value < 0) return 0;
        if (value > 100) return 100;
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
