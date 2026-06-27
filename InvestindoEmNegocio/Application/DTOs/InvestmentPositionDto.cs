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
    List<InvestmentMovementDto> Movements,
    string Currency = "BRL",
    string? MarketSymbol = null,
    decimal? MarketPrice = null,
    decimal? MarketChangePercent = null,
    string? MarketName = null,
    string? MarketLogoUrl = null,
    string? MarketSource = null,
    string? MarketProvider = null);
