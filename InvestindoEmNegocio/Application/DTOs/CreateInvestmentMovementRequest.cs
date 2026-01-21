using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CreateInvestmentMovementRequest(
    InvestmentMovementType Type,
    decimal Quantity,
    decimal Price,
    DateOnly Date,
    string? Note);
