using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CreateInvestmentPositionRequest(
    InvestmentType Type,
    string Asset,
    decimal Quantity,
    decimal AvgPrice,
    DateOnly OpenedAt,
    string Account,
    string Category,
    string? Note);
