using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IGoalOccurrenceService
{
    /// <summary>Garante as ocorrências até hoje e retorna o histórico (mais recente primeiro).</summary>
    Task<IReadOnlyList<GoalOccurrenceResponse>?> EnsureAndListAsync(Guid userId, Guid goalId, CancellationToken ct = default);

    /// <summary>Edita o alvo apenas da ocorrência corrente (não da série).</summary>
    Task<bool> OverrideCurrentTargetAsync(Guid userId, Guid goalId, decimal targetAmount, CancellationToken ct = default);
}
