using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CreateGoalRequest(
    string Title,
    decimal TargetAmount,
    int Year,
    string? Description,
    GoalStatus Status,
    decimal CurrentAmount,
    decimal ExpectedMonthly,
    DateOnly? TargetDate,
    GoalKind Kind = GoalKind.General,
    // Fase B — planejamento (opcionais, retrocompatíveis)
    GoalMode? Mode = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    RecurrenceType Recurrence = RecurrenceType.None,
    decimal? WarningThreshold = null,
    decimal? CriticalThreshold = null,
    IReadOnlyList<GoalScopeDto>? Scopes = null);
