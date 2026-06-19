namespace InvestindoEmNegocio.Application.DTOs;

public record ScenarioSimulationRequest(
    string Period,
    DateOnly? ReferenceDate,
    decimal ExtraMonthlyIncome,
    decimal ExtraMonthlyExpense,
    decimal SavingsRatePercent);

public record ScenarioSimulationResponse(
    string Period,
    DateOnly ReferenceDate,
    decimal BaseProjectedClosingBalance,
    decimal ScenarioProjectedClosingBalance,
    decimal ImpactAmount,
    decimal MonthlySavingsPotential,
    IReadOnlyList<ScenarioProjectionPointResponse> BasePoints,
    IReadOnlyList<ScenarioProjectionPointResponse> ScenarioPoints);

public record ScenarioProjectionPointResponse(
    DateOnly Date,
    decimal BaseClosingBalance,
    decimal ScenarioClosingBalance);
