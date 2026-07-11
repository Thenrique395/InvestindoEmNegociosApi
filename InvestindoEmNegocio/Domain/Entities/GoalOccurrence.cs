using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

/// <summary>
/// Uma ocorrência (período) de uma meta recorrente. Cada período gera uma
/// ocorrência com snapshot do valor-alvo, preservando o histórico. Metas de
/// período único também têm exatamente uma ocorrência.
/// </summary>
public class GoalOccurrence
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GoalId { get; private set; }
    public int Sequence { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    /// <summary>Snapshot do valor-alvo no momento em que a ocorrência foi aberta.</summary>
    public decimal TargetAmount { get; private set; }
    public GoalOccurrenceStatus Status { get; private set; } = GoalOccurrenceStatus.Active;
    public DateTime? ClosedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private GoalOccurrence() { }

    public GoalOccurrence(Guid goalId, int sequence, DateOnly periodStart, DateOnly periodEnd, decimal targetAmount)
    {
        GoalId = goalId;
        Sequence = sequence;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TargetAmount = targetAmount;
    }

    public void Close(DateTime nowUtc)
    {
        if (Status == GoalOccurrenceStatus.Closed) return;
        Status = GoalOccurrenceStatus.Closed;
        ClosedAt = nowUtc;
    }

    /// <summary>Ajusta o alvo apenas desta ocorrência (edição pontual, não da série).</summary>
    public void OverrideTarget(decimal targetAmount)
    {
        TargetAmount = targetAmount;
    }

    public bool Contains(DateOnly date) => date >= PeriodStart && date <= PeriodEnd;
}
