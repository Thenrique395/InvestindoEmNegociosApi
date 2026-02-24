using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IRobotSettingsRepository
{
    Task<RobotSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task<RobotSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task AddAsync(RobotSettings settings, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
