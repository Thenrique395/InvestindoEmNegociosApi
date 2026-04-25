using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface INotificationQueryService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default);
}
