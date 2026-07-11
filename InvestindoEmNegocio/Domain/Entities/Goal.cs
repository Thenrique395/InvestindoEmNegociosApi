using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class Goal : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal TargetAmount { get; private set; }
    public decimal CurrentAmount { get; private set; }
    public int Year { get; private set; }
    public decimal ExpectedMonthly { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public string? Description { get; private set; }
    public GoalStatus Status { get; private set; } = GoalStatus.Planned;
    public GoalKind Kind { get; private set; } = GoalKind.General;

    // Fase A — modelo de planejamento
    public GoalMode Mode { get; private set; } = GoalMode.Target;
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public RecurrenceType Recurrence { get; private set; } = RecurrenceType.None;
    /// <summary>Limiar de atenção (0–100). Ex.: 70 = avisar ao usar 70% do limite.</summary>
    public decimal? WarningThreshold { get; private set; }
    /// <summary>Limiar crítico (0–100). Ex.: 90.</summary>
    public decimal? CriticalThreshold { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    private readonly List<GoalScope> _scopes = new();
    public IReadOnlyCollection<GoalScope> Scopes => _scopes.AsReadOnly();

    private Goal() { }

    public Goal(Guid userId, Guid spaceId, string title, decimal targetAmount, int year, string? description = null, GoalStatus status = GoalStatus.Planned, decimal currentAmount = 0, decimal expectedMonthly = 0, DateOnly? targetDate = null, GoalKind kind = GoalKind.General)
    {
        UserId = userId;
        SpaceId = spaceId;
        Title = title;
        TargetAmount = targetAmount;
        Year = year;
        Description = description;
        Status = status;
        CurrentAmount = currentAmount;
        ExpectedMonthly = expectedMonthly;
        TargetDate = targetDate;
        Kind = kind;
        Mode = DefaultModeFor(kind);
    }

    public static GoalMode DefaultModeFor(GoalKind kind) => kind switch
    {
        GoalKind.Expense => GoalMode.Limit,
        GoalKind.Income => GoalMode.Target,
        GoalKind.Investment => GoalMode.RecurringContribution,
        _ => GoalMode.Target
    };

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

    /// <summary>Define os atributos de planejamento (período, modalidade, limiares).</summary>
    public void ConfigurePlanning(GoalMode mode, DateOnly? startDate, DateOnly? endDate, RecurrenceType recurrence, decimal? warningThreshold, decimal? criticalThreshold)
    {
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            throw new ArgumentException("Data final não pode ser anterior à inicial.");
        if (warningThreshold is < 0 or > 100) throw new ArgumentException("Limiar de atenção deve estar entre 0 e 100.");
        if (criticalThreshold is < 0 or > 100) throw new ArgumentException("Limiar crítico deve estar entre 0 e 100.");
        if (warningThreshold.HasValue && criticalThreshold.HasValue && warningThreshold.Value > criticalThreshold.Value)
            throw new ArgumentException("Limiar de atenção não pode ser maior que o crítico.");

        Mode = mode;
        StartDate = startDate;
        EndDate = endDate;
        Recurrence = recurrence;
        WarningThreshold = warningThreshold;
        CriticalThreshold = criticalThreshold;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceScopes(IEnumerable<GoalScope> scopes)
    {
        _scopes.Clear();
        _scopes.AddRange(scopes);
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

    // ---- Ciclo de vida ------------------------------------------------------

    public void Pause()
    {
        if (Status is GoalStatus.Completed or GoalStatus.Canceled or GoalStatus.Archived) return;
        Status = GoalStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Resume()
    {
        if (Status != GoalStatus.Paused) return;
        Status = GoalStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive(DateTime nowUtc)
    {
        Status = GoalStatus.Archived;
        ArchivedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Conclusão manual. Não aplicável a meta de despesa (consumir limite não é sucesso).
    /// </summary>
    public void CompleteManually()
    {
        if (Kind == GoalKind.Expense)
            throw new InvalidOperationException("Meta de despesas não é concluída manualmente.");
        Status = GoalStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
