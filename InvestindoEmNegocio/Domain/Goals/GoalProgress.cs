using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Goals;

/// <summary>
/// Progresso CALCULADO de uma meta (nunca persistido). Reflete sempre os
/// lançamentos reais no momento da leitura.
/// </summary>
public sealed record GoalProgress(
    decimal Target,
    /// <summary>Valor efetivado (pago/recebido/aportado). Base do progresso oficial.</summary>
    decimal Realized,
    /// <summary>Valor pendente (previsto), exibido à parte — não entra no oficial.</summary>
    decimal Pending,
    /// <summary>Percentual oficial (Realized/Target*100), sem teto.</summary>
    decimal Percent,
    /// <summary>Despesa: disponível; Receita/Investimento: quanto falta.</summary>
    decimal Remaining,
    /// <summary>Projeção simples do valor ao fim do período (ritmo atual). Nulo sem período.</summary>
    decimal? Forecast,
    int? DaysRemaining,
    CalculatedGoalState State);
