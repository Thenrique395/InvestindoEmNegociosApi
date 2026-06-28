using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ISpaceService
{
    Task<List<SpaceResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SpaceResponse> CreateAsync(Guid userId, SpaceRequest request, CancellationToken cancellationToken = default);
    Task<SpaceResponse?> UpdateAsync(Guid userId, Guid spaceId, SpaceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken = default);
    Task<AuthResponse> EnterAsync(Guid userId, Guid spaceId, EnterSpaceRequest request, CancellationToken cancellationToken = default);
}
