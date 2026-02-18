using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class B3MarketDataProvider : IMarketDataProvider
{
    public string Name => "b3";
    public bool IsEstimated => false;

    public Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Provider B3 ainda nao configurado. Use MarketData:Provider=free.");

    public Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Provider B3 ainda nao configurado. Use MarketData:Provider=free.");

    public Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Provider B3 ainda nao configurado. Use MarketData:Provider=free.");
}
