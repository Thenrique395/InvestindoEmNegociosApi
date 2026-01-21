using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IGoalContributionsService
{
    Task<IReadOnlyList<GoalContributionResponse>?> ListAsync(Guid userId, Guid goalId, CancellationToken cancellationToken = default);
    Task<GoalContributionResponse?> CreateAsync(Guid userId, Guid goalId, GoalContributionRequest request, CancellationToken cancellationToken = default);
}
