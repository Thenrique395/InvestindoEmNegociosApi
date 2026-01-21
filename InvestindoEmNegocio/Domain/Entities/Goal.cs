using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Goal
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal TargetAmount { get; private set; }
    public decimal CurrentAmount { get; private set; }
    public int Year { get; private set; }
    public decimal ExpectedMonthly { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public string? Description { get; private set; }
    public GoalStatus Status { get; private set; } = GoalStatus.Planned;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private Goal() { }

    public Goal(Guid userId, string title, decimal targetAmount, int year, string? description = null, GoalStatus status = GoalStatus.Planned, decimal currentAmount = 0, decimal expectedMonthly = 0, DateOnly? targetDate = null)
    {
        UserId = userId;
        Title = title;
        TargetAmount = targetAmount;
        Year = year;
        Description = description;
        Status = status;
        CurrentAmount = currentAmount;
        ExpectedMonthly = expectedMonthly;
        TargetDate = targetDate;
    }

    public void Update(string title, decimal targetAmount, int year, string? description, GoalStatus status, decimal currentAmount, decimal expectedMonthly, DateOnly? targetDate)
    {
        Title = title;
        TargetAmount = targetAmount;
        Year = year;
        Description = description;
        Status = status;
        CurrentAmount = currentAmount;
        ExpectedMonthly = expectedMonthly;
        TargetDate = targetDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddContribution(decimal amount)
    {
        if (amount <= 0) return;
        CurrentAmount += amount;
        if (CurrentAmount >= TargetAmount && TargetAmount > 0)
        {
            CurrentAmount = TargetAmount;
            Status = GoalStatus.Completed;
        }
        else if (Status != GoalStatus.Canceled)
        {
            Status = GoalStatus.InProgress;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAmountAndStatus(decimal newAmount, GoalStatus newStatus)
    {
        CurrentAmount = newAmount;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
