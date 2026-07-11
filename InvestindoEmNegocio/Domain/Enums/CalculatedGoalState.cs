namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// Estado de acompanhamento CALCULADO (não persistido) a partir do progresso e
/// das datas da meta. Camada sobre o <see cref="GoalStatus"/> persistido.
/// </summary>
public enum CalculatedGoalState
{
    /// <summary>Dentro do esperado.</summary>
    OnTrack,
    /// <summary>Atingiu o limiar de atenção (Warning).</summary>
    Attention,
    /// <summary>Despesa: limite ultrapassado (&gt;100%).</summary>
    Exceeded,
    /// <summary>Receita/Investimento: período encerrado sem atingir o alvo.</summary>
    Overdue,
    /// <summary>Receita/Investimento: alvo atingido (&gt;=100%).</summary>
    Achieved
}
