using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface INotificationsService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default);
    Task<int> GenerateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
}
