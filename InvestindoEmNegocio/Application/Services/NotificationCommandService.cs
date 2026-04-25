using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class NotificationCommandService(
    IUserNotificationRepository notificationRepository) : INotificationCommandService
{
    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var item = await notificationRepository.GetByIdAsync(notificationId, userId, cancellationToken);
        if (item is null)
            return;

        item.MarkAsRead();
        await notificationRepository.SaveChangesAsync(cancellationToken);
    }
}
