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
    DateOnly? TargetDate);
