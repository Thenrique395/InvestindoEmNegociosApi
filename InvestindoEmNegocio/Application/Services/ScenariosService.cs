using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public class ScenariosService(ICashflowProjectionEngine cashflowEngine) : IScenariosService
{
    public async Task<ScenarioSimulationResponse> SimulateAsync(Guid userId, ScenarioSimulationRequest request, CancellationToken cancellationToken = default)
    {
        var period = string.IsNullOrWhiteSpace(request.Period) ? "month" : request.Period;
        var baseProjection = await cashflowEngine.ProjectAsync(userId, period, request.ReferenceDate, cancellationToken);

        var dailyExtraIncome = request.ExtraMonthlyIncome / 30m;
        var dailyExtraExpense = request.ExtraMonthlyExpense / 30m;

        var basePoints = baseProjection.Points.ToList();
        var scenarioPoints = new List<ScenarioProjectionPointResponse>(basePoints.Count);

        decimal cumulativeImpact = 0m;
        foreach (var point in basePoints)
        {
            cumulativeImpact += dailyExtraIncome - dailyExtraExpense;
            scenarioPoints.Add(new ScenarioProjectionPointResponse(
                point.Date,
                point.ClosingBalance,
                point.ClosingBalance + cumulativeImpact));
        }

        var projectionDays = basePoints.Count > 0 ? basePoints.Count : 30;
        var monthlySavings = (request.ExtraMonthlyIncome - request.ExtraMonthlyExpense) +
                             (baseProjection.OpeningBalance * request.SavingsRatePercent / 100m / 12m);

        var scenarioClosing = baseProjection.ProjectedClosingBalance + (dailyExtraIncome - dailyExtraExpense) * projectionDays;

        return new ScenarioSimulationResponse(
            baseProjection.Period,
            baseProjection.ReferenceDate,
            baseProjection.ProjectedClosingBalance,
            scenarioClosing,
            scenarioClosing - baseProjection.ProjectedClosingBalance,
            monthlySavings,
            basePoints.Select(p => new ScenarioProjectionPointResponse(p.Date, p.ClosingBalance, p.ClosingBalance)).ToList(),
            scenarioPoints);
    }
}
