using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminCardBrandsService
{
    Task<IReadOnlyList<CardBrandAdminResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<CardBrandAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task<CardBrandAdminResponse> CreateAsync(CreateCardBrandRequest request, CancellationToken cancellationToken = default);
}
