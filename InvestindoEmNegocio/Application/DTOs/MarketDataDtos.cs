namespace InvestindoEmNegocio.Application.DTOs;

public sealed record MarketQuoteResponse(
    string Symbol,
    decimal? Price,
    decimal? ChangePercent,
    string Currency,
    string? Name,
    DateTimeOffset? LastUpdatedUtc,
    string Source,
    bool IsEstimated,
    string ProviderLabel);

public sealed record MarketProfileResponse(
    string Symbol,
    string? Name,
    string? Sector,
    string? Industry,
    string? Website,
    string? LogoUrl,
    string Source,
    bool IsEstimated,
    string ProviderLabel);

public sealed record MarketHistoryPointResponse(DateOnly Date, decimal Close);

public sealed record MarketHistoryResponse(
    string Symbol,
    string Period,
    string Source,
    bool IsEstimated,
    string ProviderLabel,
    IReadOnlyList<MarketHistoryPointResponse> Points);

public sealed record MarketSnapshotResponse(
    string Symbol,
    decimal? Price,
    decimal? ChangePercent,
    string Currency,
    string? Name,
    string? LogoUrl,
    DateTimeOffset? LastUpdatedUtc,
    string Source,
    bool IsEstimated,
    string ProviderLabel);
