using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record GoalResponse(
    Guid Id,
    string Title,
    decimal TargetAmount,
    decimal CurrentAmount,
    int Year,
    string? Description,
    GoalStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal ExpectedMonthly,
    DateOnly? TargetDate,
    GoalKind Kind = GoalKind.General,
    // Fase B — planejamento
    GoalMode Mode = GoalMode.Target,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    RecurrenceType Recurrence = RecurrenceType.None,
    decimal? WarningThreshold = null,
    decimal? CriticalThreshold = null,
    DateTime? ArchivedAt = null,
    IReadOnlyList<GoalScopeDto>? Scopes = null);
