using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IB3Connector
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    Task<B3ImportSnapshot?> GetLatestSnapshotAsync(Guid userId, CancellationToken cancellationToken);
}
