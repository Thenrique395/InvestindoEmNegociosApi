using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IB3SyncService
{
    Task<B3ConsentStatusResponse> GetConsentStatusAsync(Guid userId, CancellationToken cancellationToken);
    Task<B3ConsentStatusResponse> GrantMockConsentAsync(Guid userId, CancellationToken cancellationToken);
    Task<B3SyncResponse> SyncAsync(Guid userId, B3SyncRequest request, CancellationToken cancellationToken);
}
