using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminNotificationSettingsService
{
    Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<NotificationSettingsDto> UpdateAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken = default);
}
