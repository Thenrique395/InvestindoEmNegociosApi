namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>Ocorrência (período) de uma meta, com o realizado apurado.</summary>
public record GoalOccurrenceResponse(
    Guid Id,
    int Sequence,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TargetAmount,
    decimal Realized,
    decimal Percent,
    string Status,
    bool IsCurrent);
