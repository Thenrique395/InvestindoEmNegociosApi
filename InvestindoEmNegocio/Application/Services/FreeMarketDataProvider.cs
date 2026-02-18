using System.Globalization;
using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

// Mantido o nome da classe para evitar mudanças de DI em vários pontos.
// A fonte agora é exclusivamente BRAPI.
public sealed class FreeMarketDataProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketDataOptions> options,
    ILogger<FreeMarketDataProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _brapiClient = httpClientFactory.CreateClient("MarketBrapi");
    private readonly MarketDataOptions _options = options.Value;

    public string Name => "free";
    public bool IsEstimated => false;

    public async Task<IReadOnlyDictionary<string, MarketSnapshotResponse>> GetSnapshotsAsync(IReadOnlyCollection<string> symbols, CancellationToken cancellationToken = default)
    {
        var normalized = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
            return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var tickers = string.Join(",", normalized);
            var path = $"api/quote/{Uri.EscapeDataString(tickers)}?fundamental=true&dividends=true&range=1d&interval=1d";
            using var request = CreateBrapiRequest(path);
            using var response = await _brapiClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("BRAPI snapshot retornou status {Status} para {Tickers}", (int)response.StatusCode, tickers);
                return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (json.RootElement.TryGetProperty("error", out var errorNode) && errorNode.ValueKind is JsonValueKind.True)
            {
                var msg = ReadString(json.RootElement, "message") ?? "Erro da BRAPI";
                logger.LogWarning("BRAPI snapshot erro para {Tickers}: {Message}", tickers, msg);
                return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);
            }

            if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);

            var dict = new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in results.EnumerateArray())
            {
                var parsed = ParseSnapshot(item);
                if (parsed is null) continue;
                dict[parsed.Symbol] = parsed;
            }

            return dict;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar snapshots BRAPI");
            return new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var result = await TryGetBrapiQuoteAsync(normalized, includeHistory: false, cancellationToken);

        if (result is null)
        {
            return new MarketQuoteResponse(
                normalized,
                null,
                null,
                "BRL",
                null,
                DateTimeOffset.UtcNow,
                "Sem resposta da BRAPI",
                false,
                "BRAPI");
        }

        return new MarketQuoteResponse(
            normalized,
            result.Value.price,
            result.Value.changePercent,
            result.Value.currency ?? "BRL",
            result.Value.name,
            result.Value.lastUpdatedUtc,
            result.Value.source,
            false,
            "BRAPI");
    }

    public async Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var result = await TryGetBrapiQuoteAsync(normalized, includeHistory: false, cancellationToken);

        if (result is null)
        {
            return new MarketProfileResponse(
                normalized,
                null,
                null,
                null,
                null,
                null,
                "Sem resposta da BRAPI",
                false,
                "BRAPI");
        }

        return new MarketProfileResponse(
            normalized,
            result.Value.name,
            null,
            null,
            null,
            result.Value.logoUrl,
            result.Value.source,
            false,
            "BRAPI");
    }

    public async Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var normalizedPeriod = NormalizePeriod(period);
        var result = await TryGetBrapiQuoteAsync(normalized, includeHistory: true, cancellationToken, normalizedPeriod);

        if (result is null)
        {
            return new MarketHistoryResponse(
                normalized,
                normalizedPeriod,
                "Sem resposta da BRAPI",
                false,
                "BRAPI",
                []);
        }

        return new MarketHistoryResponse(
            normalized,
            normalizedPeriod,
            result.Value.source,
            false,
            "BRAPI",
            result.Value.historyPoints);
    }

    private async Task<(decimal? price, decimal? changePercent, string? currency, string? name, DateTimeOffset? lastUpdatedUtc, string? logoUrl, string source, List<MarketHistoryPointResponse> historyPoints)?> TryGetBrapiQuoteAsync(
        string symbol,
        bool includeHistory,
        CancellationToken cancellationToken,
        string? period = null)
    {
        try
        {
            var qs = new List<string>();
            if (includeHistory)
            {
                qs.Add($"range={Uri.EscapeDataString(period ?? "6mo")}");
                qs.Add("interval=1d");
            }

            var suffix = qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty;
            var path = $"api/quote/{Uri.EscapeDataString(symbol)}{suffix}";
            using var request = CreateBrapiRequest(path);
            using var response = await _brapiClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("BRAPI retornou status {Status} para {Symbol}", (int)response.StatusCode, symbol);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (json.RootElement.TryGetProperty("error", out var errorNode) && errorNode.ValueKind is JsonValueKind.True)
            {
                var msg = ReadString(json.RootElement, "message") ?? "Erro da BRAPI";
                logger.LogWarning("BRAPI erro para {Symbol}: {Message}", symbol, msg);
                return null;
            }

            if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var snapshot = ParseSnapshot(first);
            if (snapshot is null) return null;

            var history = new List<MarketHistoryPointResponse>();
            if (includeHistory && first.TryGetProperty("historicalDataPrice", out var hist) && hist.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in hist.EnumerateArray())
                {
                    if (!item.TryGetProperty("date", out var d) || d.ValueKind != JsonValueKind.Number || !d.TryGetInt64(out var ts)) continue;
                    var close = ReadDecimal(item, "close");
                    if (close is null) continue;
                    var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(ts).DateTime);
                    history.Add(new MarketHistoryPointResponse(date, close.Value));
                }
            }

            return (snapshot.Price, snapshot.ChangePercent, snapshot.Currency, snapshot.Name, snapshot.LastUpdatedUtc, snapshot.LogoUrl, "BRAPI /api/quote", history);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar BRAPI para {Symbol}", symbol);
            return null;
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Símbolo é obrigatório.", nameof(symbol));
        return symbol.Trim().ToUpperInvariant();
    }

    private static string NormalizePeriod(string period)
        => period?.Trim().ToLowerInvariant() switch
        {
            "1mo" or "3mo" or "6mo" or "1y" or "2y" or "5y" => period.Trim().ToLowerInvariant(),
            _ => "6mo"
        };

    private HttpRequestMessage CreateBrapiRequest(string path)
    {
        var token = _options.BrapiToken?.Trim();
        var finalPath = path;

        // Mantém compatibilidade com os dois formatos aceitos pela BRAPI:
        // Authorization Bearer e query string token=.
        if (!string.IsNullOrWhiteSpace(token) &&
            !finalPath.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            finalPath = finalPath.Contains('?', StringComparison.Ordinal)
                ? $"{finalPath}&token={Uri.EscapeDataString(token)}"
                : $"{finalPath}?token={Uri.EscapeDataString(token)}";
        }

        var req = new HttpRequestMessage(HttpMethod.Get, finalPath);
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return req;
    }

    private static MarketSnapshotResponse? ParseSnapshot(JsonElement element)
    {
        var symbol = ReadString(element, "symbol");
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        var price = ReadDecimal(element, "regularMarketPrice");
        var change = ReadDecimal(element, "regularMarketChangePercent");
        var currency = ReadString(element, "currency") ?? "BRL";
        var name = ReadString(element, "longName") ?? ReadString(element, "shortName") ?? symbol;
        var logo = ReadString(element, "logourl");
        var updated = ReadDateTimeOffset(element, "regularMarketTime");

        return new MarketSnapshotResponse(
            symbol.ToUpperInvariant(),
            price,
            change,
            currency,
            name,
            logo,
            updated,
            "BRAPI /api/quote",
            false,
            "BRAPI");
    }

    private static decimal? ReadDecimal(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        if (value.TryGetDecimal(out var number)) return number;
        if (value.TryGetDouble(out var asDouble)) return (decimal)asDouble;
        return null;
    }

    private static string? ReadString(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement node, string property)
    {
        var raw = ReadString(node, property);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)) return dto;
        return null;
    }
}
