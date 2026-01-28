using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationKind Kind,
    MoneyType? MoneyType,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? ReadAt
);
