using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class MarketDataService(
    IMemoryCache cache,
    IEnumerable<IMarketDataProvider> providers,
    IOptions<MarketDataOptions> options,
    ILogger<MarketDataService> logger) : IMarketDataService
{
    private readonly MarketDataOptions _options = options.Value;

    public Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var cacheKey = $"market:quote:{ProviderName()}:{normalized}";
        return cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.QuoteCacheMinutes, 1));
            var provider = ResolveProvider();
            return await provider.GetQuoteAsync(normalized, cancellationToken);
        })!;
    }

    public Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var cacheKey = $"market:profile:{ProviderName()}:{normalized}";
        return cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.ProfileCacheMinutes, 1));
            var provider = ResolveProvider();
            return await provider.GetProfileAsync(normalized, cancellationToken);
        })!;
    }

    public Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var normalizedPeriod = NormalizePeriod(period);
        var cacheKey = $"market:history:{ProviderName()}:{normalized}:{normalizedPeriod}";
        return cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.HistoryCacheMinutes, 1));
            var provider = ResolveProvider();
            return await provider.GetHistoryAsync(normalized, normalizedPeriod, cancellationToken);
        })!;
    }

    public async Task<IReadOnlyDictionary<string, MarketSnapshotResponse>> GetSnapshotsAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0) return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);

        var normalized = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0) return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = $"market:snapshots:{ProviderName()}:{string.Join(',', normalized)}";
        return await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(_options.QuoteCacheMinutes, 1));
            var provider = ResolveProvider();
            var snapshots = await provider.GetSnapshotsAsync(normalized, cancellationToken);
            return new Dictionary<string, MarketSnapshotResponse>(snapshots, StringComparer.OrdinalIgnoreCase);
        }) ?? new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);
    }

    private IMarketDataProvider ResolveProvider()
    {
        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, ProviderName(), StringComparison.OrdinalIgnoreCase));
        if (provider is not null) return provider;

        logger.LogWarning("Provider de market data '{Provider}' nao encontrado. Fallback para 'free'.", ProviderName());
        return providers.First(p => string.Equals(p.Name, "free", StringComparison.OrdinalIgnoreCase));
    }

    private string ProviderName() => string.IsNullOrWhiteSpace(_options.Provider) ? "free" : _options.Provider.Trim().ToLowerInvariant();

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Símbolo é obrigatório.", nameof(symbol));
        return symbol.Trim().ToUpperInvariant();
    }

    private static string NormalizePeriod(string period)
    {
        return period?.Trim().ToLowerInvariant() switch
        {
            "1mo" or "3mo" or "6mo" or "1y" or "2y" or "5y" => period.Trim().ToLowerInvariant(),
            _ => "6mo"
        };
    }
}
