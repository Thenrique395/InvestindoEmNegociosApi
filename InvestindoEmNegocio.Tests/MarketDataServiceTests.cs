using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class MarketDataServiceTests
{
    [Fact]
    public async Task GetQuoteAsync_Should_Normalize_Symbol_And_Cache_Result()
    {
        var provider = new FakeMarketDataProvider("free");
        var sut = BuildSut(new[] { provider }, new MarketDataOptions { Provider = "free", QuoteCacheMinutes = 10 });

        var first = await sut.GetQuoteAsync(" petr4 ", CancellationToken.None);
        var second = await sut.GetQuoteAsync("PETR4", CancellationToken.None);

        first.Symbol.Should().Be("PETR4");
        second.Symbol.Should().Be("PETR4");
        provider.QuoteCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryAsync_Should_Default_Period_When_Invalid()
    {
        var provider = new FakeMarketDataProvider("free");
        var sut = BuildSut(new[] { provider }, new MarketDataOptions { Provider = "free", HistoryCacheMinutes = 10 });

        var result = await sut.GetHistoryAsync("VALE3", "invalid", CancellationToken.None);

        result.Period.Should().Be("6mo");
        provider.LastHistoryPeriod.Should().Be("6mo");
    }

    [Fact]
    public async Task GetProfileAsync_Should_Fallback_To_Free_When_Configured_Provider_Not_Found()
    {
        var freeProvider = new FakeMarketDataProvider("free");
        var sut = BuildSut(new[] { freeProvider }, new MarketDataOptions { Provider = "brapi", ProfileCacheMinutes = 10 });

        var result = await sut.GetProfileAsync("ITSA4", CancellationToken.None);

        result.Symbol.Should().Be("ITSA4");
        freeProvider.ProfileCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetSnapshotsAsync_Should_Return_Empty_When_Input_Is_Empty()
    {
        var provider = new FakeMarketDataProvider("free");
        var sut = BuildSut(new[] { provider }, new MarketDataOptions { Provider = "free" });

        var result = await sut.GetSnapshotsAsync(Array.Empty<string>(), CancellationToken.None);

        result.Should().BeEmpty();
        provider.SnapshotCalls.Should().Be(0);
    }

    private static MarketDataService BuildSut(IEnumerable<IMarketDataProvider> providers, MarketDataOptions options)
    {
        return new MarketDataService(
            new MemoryCache(new MemoryCacheOptions()),
            providers,
            Options.Create(options),
            NullLogger<MarketDataService>.Instance);
    }

    private sealed class FakeMarketDataProvider(string name) : IMarketDataProvider
    {
        public string Name { get; } = name;
        public bool IsEstimated => false;
        public int QuoteCalls { get; private set; }
        public int ProfileCalls { get; private set; }
        public int SnapshotCalls { get; private set; }
        public string? LastHistoryPeriod { get; private set; }

        public Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
        {
            QuoteCalls++;
            return Task.FromResult(new MarketQuoteResponse(symbol, 10, 1, "BRL", symbol, DateTimeOffset.UtcNow, "fake", false, "fake"));
        }

        public Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default)
        {
            ProfileCalls++;
            return Task.FromResult(new MarketProfileResponse(symbol, symbol, null, null, null, null, "fake", false, "fake"));
        }

        public Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default)
        {
            LastHistoryPeriod = period;
            return Task.FromResult(new MarketHistoryResponse(symbol, period, "fake", false, "fake", []));
        }

        public Task<IReadOnlyDictionary<string, MarketSnapshotResponse>> GetSnapshotsAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken = default)
        {
            SnapshotCalls++;
            var data = symbols.ToDictionary(
                s => s,
                s => new MarketSnapshotResponse(s, 10, 1, "BRL", s, null, DateTimeOffset.UtcNow, "fake", false, "fake"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, MarketSnapshotResponse>>(data);
        }
    }
}
