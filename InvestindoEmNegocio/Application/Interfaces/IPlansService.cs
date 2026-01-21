using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IPlansService
{
    Task<PlanResponse> CreateAsync(Guid userId, CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanResponse>> ListAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default);
    Task<PlanDetailsResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<PlanResponse?> UpdateAsync(Guid userId, Guid id, CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
