using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IGoalProgressService
{
    /// <summary>Calcula o progresso de uma meta a partir dos lançamentos reais.</summary>
    Task<GoalProgressResponse?> GetProgressAsync(Guid userId, Guid goalId, CancellationToken cancellationToken = default);
}
