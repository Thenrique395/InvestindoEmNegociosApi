using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICategoriesService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(Guid userId, MoneyType? appliesTo, CancellationToken cancellationToken = default);
    Task<CategoryResponse> CreateAsync(Guid userId, UpsertCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse?> UpdateAsync(Guid userId, Guid id, UpsertCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
