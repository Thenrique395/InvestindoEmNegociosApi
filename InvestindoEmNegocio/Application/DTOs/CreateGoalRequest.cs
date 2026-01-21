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
    DateOnly? TargetDate);
