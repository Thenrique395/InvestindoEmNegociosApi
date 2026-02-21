using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminCategoriesService
{
    Task<IReadOnlyList<AdminCategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<AdminCategoryResponse> CreateAsync(AdminCategoryRequest request, CancellationToken cancellationToken);
    Task<AdminCategoryResponse> UpdateAsync(Guid id, AdminCategoryRequest request, CancellationToken cancellationToken);
    Task<AdminCategoryResponse> UpdateStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
