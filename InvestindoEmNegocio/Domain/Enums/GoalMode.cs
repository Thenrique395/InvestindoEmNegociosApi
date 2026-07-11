namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// Modalidade de cálculo da meta, específica por tipo (Kind).
/// Expense/Income usam Limit/Target; Investment tem modalidades de aporte.
/// </summary>
public enum GoalMode
{
    /// <summary>Meta de despesas: limite máximo de gasto.</summary>
    Limit,
    /// <summary>Meta de receitas: valor-alvo a receber.</summary>
    Target,
    /// <summary>Investimento: aporte recorrente por período.</summary>
    RecurringContribution,
    /// <summary>Investimento: aporte total no período.</summary>
    PeriodContribution,
    /// <summary>Investimento: valor acumulado (principal aportado).</summary>
    AccumulatedValue
}
