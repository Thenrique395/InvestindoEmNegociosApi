using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IGoalsService
{
    Task<IReadOnlyList<GoalResponse>> ListAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default);
    Task<GoalResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<GoalResponse> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalResponse?> UpdateAsync(Guid userId, Guid id, CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
