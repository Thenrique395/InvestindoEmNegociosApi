using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Goals;

/// <summary>
/// Cálculo puro do progresso de uma meta a partir de valores já apurados
/// (efetivado × pendente) e do período. Sem acesso a banco — 100% testável.
///
/// Semântica por tipo:
///  - Despesa (Limit): consumir o limite NÃO é sucesso. &gt;100% = Excedida.
///  - Receita/Investimento (Target/aporte): aproximar-se do alvo é positivo.
/// </summary>
public static class GoalProgressCalculator
{
    private const decimal DefaultWarning = 80m;
    private const decimal DefaultCritical = 100m;

    public static GoalProgress Calculate(
        GoalKind kind,
        decimal target,
        decimal realized,
        decimal pending,
        DateOnly? start,
        DateOnly? end,
        DateOnly today,
        decimal? warningThreshold = null,
        decimal? criticalThreshold = null)
    {
        realized = Math.Max(0m, realized);
        pending = Math.Max(0m, pending);
        var percent = target > 0 ? Math.Round(realized / target * 100m, 2) : 0m;

        var warning = warningThreshold ?? DefaultWarning;

        int? daysRemaining = end.HasValue
            ? Math.Max(0, end.Value.DayNumber - today.DayNumber)
            : null;

        var forecast = ProjectForecast(realized, start, end, today);

        var isExpense = kind == GoalKind.Expense;
        var remaining = Math.Max(target - realized, 0m);

        var state = isExpense
            ? ExpenseState(percent, warning)
            : TargetState(percent, realized, target, start, end, today, warning);

        return new GoalProgress(target, realized, pending, percent, remaining, forecast, daysRemaining, state);
    }

    private static CalculatedGoalState ExpenseState(decimal percent, decimal warning)
    {
        if (percent > 100m) return CalculatedGoalState.Exceeded;
        if (percent >= warning) return CalculatedGoalState.Attention;
        return CalculatedGoalState.OnTrack;
    }

    private static CalculatedGoalState TargetState(
        decimal percent, decimal realized, decimal target,
        DateOnly? start, DateOnly? end, DateOnly today, decimal warning)
    {
        if (percent >= 100m) return CalculatedGoalState.Achieved;
        if (end.HasValue && today > end.Value) return CalculatedGoalState.Overdue;

        // Abaixo do ritmo necessário para o tempo já decorrido → atenção.
        if (start.HasValue && end.HasValue && target > 0)
        {
            var totalDays = end.Value.DayNumber - start.Value.DayNumber;
            var elapsed = Math.Clamp(today.DayNumber - start.Value.DayNumber, 0, Math.Max(totalDays, 0));
            if (totalDays > 0)
            {
                var expectedByNow = target * ((decimal)elapsed / totalDays);
                var floor = expectedByNow * (warning / 100m);
                if (realized < floor) return CalculatedGoalState.Attention;
            }
        }
        return CalculatedGoalState.OnTrack;
    }

    private static decimal? ProjectForecast(decimal realized, DateOnly? start, DateOnly? end, DateOnly today)
    {
        if (!start.HasValue || !end.HasValue) return null;
        var totalDays = end.Value.DayNumber - start.Value.DayNumber + 1;
        var elapsed = today.DayNumber - start.Value.DayNumber + 1;
        if (totalDays <= 0 || elapsed <= 0) return null;
        if (elapsed >= totalDays) return Math.Round(realized, 2);
        return Math.Round(realized / elapsed * totalDays, 2);
    }
}
