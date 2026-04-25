using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class NotificationQueryService(
    IUserNotificationRepository notificationRepository) : INotificationQueryService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default)
    {
        var items = await notificationRepository.ListByUserAsync(userId, unreadOnly, limit, cancellationToken);
        return items.Select(NotificationDtoMapper.ToDto).ToList();
    }
}
