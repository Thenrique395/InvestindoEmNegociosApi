namespace InvestindoEmNegocio.Domain.Entities;

public class GoalContribution
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GoalId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private GoalContribution() { }

    public GoalContribution(Guid goalId, Guid userId, decimal amount, DateOnly date, string? note = null)
    {
        GoalId = goalId;
        UserId = userId;
        Amount = amount;
        Date = date;
        Note = note;
    }
}
