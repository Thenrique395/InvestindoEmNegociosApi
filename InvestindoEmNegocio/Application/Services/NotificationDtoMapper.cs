using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class NotificationDtoMapper
{
    internal static NotificationDto ToDto(UserNotification notification)
    {
        JsonElement? payload = null;
        if (!string.IsNullOrWhiteSpace(notification.PayloadJson))
        {
            using var document = JsonDocument.Parse(notification.PayloadJson);
            payload = JsonSerializer.Deserialize<JsonElement>(document.RootElement.GetRawText());
        }

        return new NotificationDto(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Kind,
            notification.MoneyType,
            notification.DueDate,
            notification.CreatedAt,
            notification.ReadAt,
            payload);
    }
}
