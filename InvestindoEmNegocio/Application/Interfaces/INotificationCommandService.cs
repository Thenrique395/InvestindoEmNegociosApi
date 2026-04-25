namespace InvestindoEmNegocio.Application.Interfaces;

public interface INotificationCommandService
{
    Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
}
