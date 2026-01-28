using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface INotificationSettingsRepository
{
    Task<NotificationSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task<NotificationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task AddAsync(NotificationSettings settings, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
