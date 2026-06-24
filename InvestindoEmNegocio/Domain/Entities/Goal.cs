using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Goal : ISoftDeletable
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
    public GoalKind Kind { get; private set; } = GoalKind.General;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    private Goal() { }

    public Goal(Guid userId, string title, decimal targetAmount, int year, string? description = null, GoalStatus status = GoalStatus.Planned, decimal currentAmount = 0, decimal expectedMonthly = 0, DateOnly? targetDate = null, GoalKind kind = GoalKind.General)
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
        Kind = kind;
    }

    public void Update(string title, decimal targetAmount, int year, string? description, GoalStatus status, decimal currentAmount, decimal expectedMonthly, DateOnly? targetDate, GoalKind kind = GoalKind.General)
    {
        Title = title;
        TargetAmount = targetAmount;
        Year = year;
        Description = description;
        Status = status;
        CurrentAmount = currentAmount;
        ExpectedMonthly = expectedMonthly;
        TargetDate = targetDate;
        Kind = kind;
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

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
