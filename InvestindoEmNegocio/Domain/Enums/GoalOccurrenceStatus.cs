namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>Estado de uma ocorrência de meta recorrente.</summary>
public enum GoalOccurrenceStatus
{
    /// <summary>Ocorrência do período corrente.</summary>
    Active,
    /// <summary>Ocorrência de período já encerrado (histórico).</summary>
    Closed
}
