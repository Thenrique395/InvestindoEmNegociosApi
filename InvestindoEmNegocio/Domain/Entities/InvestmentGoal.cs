namespace InvestindoEmNegocio.Domain.Entities;

public class InvestmentGoal
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public decimal TargetAmount { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private InvestmentGoal() { }

    public InvestmentGoal(Guid userId, decimal targetAmount)
    {
        UserId = userId;
        SetTargetAmount(targetAmount);
    }

    public void SetTargetAmount(decimal targetAmount)
    {
        TargetAmount = targetAmount;
        UpdatedAt = DateTime.UtcNow;
    }
}
