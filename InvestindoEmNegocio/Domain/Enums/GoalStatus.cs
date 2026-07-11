namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// Estados PERSISTIDOS da meta (ações do usuário/sistema).
/// Estados de acompanhamento (Em atenção, Excedida, Atrasada, Atingida) são
/// CALCULADOS a partir do progresso/datas — ver <see cref="CalculatedGoalState"/>.
/// Valores legados (Planned/InProgress/Completed/Canceled) preservados.
/// </summary>
public enum GoalStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Canceled = 3,
    Draft = 4,
    Scheduled = 5,
    Active = 6,
    Paused = 7,
    Archived = 8
}
