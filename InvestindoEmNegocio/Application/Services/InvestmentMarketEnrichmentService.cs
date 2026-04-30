using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InvestmentMarketEnrichmentService(
    IMarketDataService marketDataService,
    ILogger<InvestmentMarketEnrichmentService> logger) : IInvestmentMarketEnrichmentService
{
    private readonly ILogger<InvestmentMarketEnrichmentService> _logger = logger;

    public async Task<List<InvestmentPositionDto>> EnrichWithMarketAsync(List<InvestmentPositionDto> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return items;

        var symbols = items
            .Select(i => InvestmentsShared.ExtractTicker(i.Asset))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (symbols.Length == 0) return items;

        IReadOnlyDictionary<string, MarketSnapshotResponse> snapshots;
        try
        {
            snapshots = await marketDataService.GetSnapshotsAsync(symbols, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enriquecer posições com dados de mercado.");
            return items;
        }

        if (snapshots.Count == 0) return items;

        return items.Select(item =>
        {
            var symbol = InvestmentsShared.ExtractTicker(item.Asset);
            if (string.IsNullOrWhiteSpace(symbol) || !snapshots.TryGetValue(symbol, out var snap))
                return item;

            return item with
            {
                MarketSymbol = snap.Symbol,
                MarketPrice = snap.Price,
                MarketChangePercent = snap.ChangePercent,
                MarketName = snap.Name,
                MarketLogoUrl = snap.LogoUrl,
                MarketSource = snap.Source,
                MarketProvider = snap.ProviderLabel
            };
        }).ToList();
    }
}
