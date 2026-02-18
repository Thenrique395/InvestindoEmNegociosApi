using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IMarketDataProvider
{
    string Name { get; }
    bool IsEstimated { get; }
    Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, MarketSnapshotResponse>> GetSnapshotsAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken = default);
}
