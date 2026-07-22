using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICategoriesService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(Guid userId, MoneyType? appliesTo, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CategoryResponse> CreateAsync(Guid userId, UpsertCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse?> UpdateAsync(Guid userId, Guid id, UpsertCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse?> SetStatusAsync(Guid userId, Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<CategoryDeletionOutcome> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
