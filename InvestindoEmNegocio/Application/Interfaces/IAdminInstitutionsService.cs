using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminInstitutionsService
{
    Task<IReadOnlyList<InstitutionAdminResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<InstitutionAdminResponse> CreateAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default);
    Task<InstitutionAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
}
