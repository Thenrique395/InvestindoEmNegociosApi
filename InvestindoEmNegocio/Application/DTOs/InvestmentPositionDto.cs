using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record InvestmentPositionDto(
    Guid Id,
    InvestmentType Type,
    string Asset,
    decimal Quantity,
    decimal AvgPrice,
    DateOnly OpenedAt,
    string Account,
    string Category,
    string? Note,
    List<InvestmentMovementDto> Movements);
