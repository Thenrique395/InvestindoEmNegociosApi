namespace InvestindoEmNegocio.Application.DTOs;

public record UpsertIncomeGoalRequest(
    int Year,
    decimal ExpectedMonthly);
