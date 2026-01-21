using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record InvestmentMovementDto(
    Guid Id,
    InvestmentMovementType Type,
    decimal Quantity,
    decimal Price,
    DateOnly Date,
    string? Note);
