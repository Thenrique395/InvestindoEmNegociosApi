namespace InvestindoEmNegocio.Application.DTOs;

public sealed record InvestmentBenchmarkItemDto(
    string Name,
    decimal ReturnPercent,
    string Source,
    bool IsEstimated);

public sealed record InvestmentBenchmarksResponse(
    int Months,
    IReadOnlyList<InvestmentBenchmarkItemDto> Items);
