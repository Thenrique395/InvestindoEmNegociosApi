using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IUserNotificationRepository
{
    Task<List<UserNotification>> ListByUserAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default);
    Task<UserNotification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, string referenceKey, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<UserNotification> notifications, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
