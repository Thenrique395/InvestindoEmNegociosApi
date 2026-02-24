using InvestindoEmNegocio.Domain.Enums;
using System.Text.Json;

namespace InvestindoEmNegocio.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationKind Kind,
    MoneyType? MoneyType,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? ReadAt,
    JsonElement? Payload
);
