using System.Globalization;
using System.Net;
using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class FreeMarketDataProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<FreeMarketDataProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _yahooClient = httpClientFactory.CreateClient("MarketYahoo");
    private readonly HttpClient _stooqClient = httpClientFactory.CreateClient("MarketStooq");

    public string Name => "free";
    public bool IsEstimated => false;

    public async Task<MarketQuoteResponse> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);

        var yahoo = await TryGetYahooQuoteAsync(normalized, cancellationToken);
        if (yahoo is not null)
        {
            return yahoo with { ProviderLabel = "Yahoo (free) + fallback Stooq" };
        }

        var stooq = await TryGetStooqQuoteAsync(normalized, cancellationToken);
        if (stooq is not null)
        {
            return stooq with { ProviderLabel = "Stooq (free)" };
        }

        throw new InvalidOperationException($"Nao foi possivel obter cotacao para {normalized}.");
    }

    public async Task<MarketProfileResponse> GetProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var yahooSymbol = ToYahooSymbol(normalized);

        var quote = await TryGetYahooQuoteSummaryAsync(yahooSymbol, cancellationToken);
        if (quote is not null)
        {
            var website = quote.Value.website;
            var logoUrl = BuildLogoUrl(website);

            return new MarketProfileResponse(
                normalized,
                quote.Value.name,
                quote.Value.sector,
                quote.Value.industry,
                website,
                logoUrl,
                "Yahoo quoteSummary",
                false,
                "Yahoo (free)");
        }

        return new MarketProfileResponse(
            normalized,
            normalized,
            null,
            null,
            null,
            null,
            "Fallback sem perfil",
            true,
            "FAKE - TEMPORARIAMENTE");
    }

    public async Task<MarketHistoryResponse> GetHistoryAsync(string symbol, string period = "6mo", CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSymbol(symbol);
        var yahoo = await TryGetYahooHistoryAsync(normalized, period, cancellationToken);
        if (yahoo is not null)
        {
            return yahoo with { ProviderLabel = "Yahoo (free)" };
        }

        throw new InvalidOperationException($"Nao foi possivel obter historico para {normalized}.");
    }

    private async Task<MarketQuoteResponse?> TryGetYahooQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var yahooSymbol = ToYahooSymbol(symbol);
            using var response = await _yahooClient.GetAsync($"v7/finance/quote?symbols={Uri.EscapeDataString(yahooSymbol)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yahoo quote retornou status {Status} para {Symbol}", (int)response.StatusCode, symbol);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var result = json.RootElement.GetProperty("quoteResponse").GetProperty("result");
            if (result.GetArrayLength() == 0) return null;

            var first = result[0];
            var price = ReadDecimal(first, "regularMarketPrice");
            if (price is null) return null;

            var changePercent = ReadDecimal(first, "regularMarketChangePercent");
            var currency = ReadString(first, "currency") ?? "BRL";
            var name = ReadString(first, "longName") ?? ReadString(first, "shortName") ?? symbol;
            var ts = ReadLong(first, "regularMarketTime");
            var updated = ts is null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(ts.Value);

            return new MarketQuoteResponse(
                symbol,
                price,
                changePercent,
                currency,
                name,
                updated,
                "Yahoo quote",
                false,
                "Yahoo (free)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no Yahoo quote para {Symbol}", symbol);
            return null;
        }
    }

    private async Task<MarketQuoteResponse?> TryGetStooqQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var stooqSymbol = ToStooqSymbol(symbol);
            var url = $"q/l/?s={Uri.EscapeDataString(stooqSymbol)}&f=sd2t2ohlcv&h&e=csv";
            using var response = await _stooqClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stooq quote retornou status {Status} para {Symbol}", (int)response.StatusCode, symbol);
                return null;
            }

            var csv = await response.Content.ReadAsStringAsync(cancellationToken);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length < 2) return null;

            var cols = lines[1].Split(',');
            if (cols.Length < 7) return null;
            var closeRaw = cols[6];
            if (!decimal.TryParse(closeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var close))
            {
                return null;
            }

            DateTimeOffset? updated = null;
            if (DateOnly.TryParse(cols[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                updated = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            }

            return new MarketQuoteResponse(
                symbol,
                close,
                null,
                "BRL",
                symbol,
                updated,
                "Stooq CSV",
                false,
                "Stooq (free)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no Stooq quote para {Symbol}", symbol);
            return null;
        }
    }

    private async Task<(string? name, string? sector, string? industry, string? website)?> TryGetYahooQuoteSummaryAsync(string yahooSymbol, CancellationToken cancellationToken)
    {
        try
        {
            var modules = Uri.EscapeDataString("assetProfile,price");
            using var response = await _yahooClient.GetAsync($"v10/finance/quoteSummary/{Uri.EscapeDataString(yahooSymbol)}?modules={modules}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yahoo quoteSummary retornou status {Status} para {Symbol}", (int)response.StatusCode, yahooSymbol);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var resultArray = json.RootElement.GetProperty("quoteSummary").GetProperty("result");
            if (resultArray.ValueKind != JsonValueKind.Array || resultArray.GetArrayLength() == 0) return null;

            var first = resultArray[0];
            var priceNode = first.TryGetProperty("price", out var p) ? p : default;
            var profileNode = first.TryGetProperty("assetProfile", out var ap) ? ap : default;

            var name = priceNode.ValueKind == JsonValueKind.Object
                ? ReadString(priceNode, "longName") ?? ReadString(priceNode, "shortName")
                : null;

            var sector = profileNode.ValueKind == JsonValueKind.Object ? ReadString(profileNode, "sector") : null;
            var industry = profileNode.ValueKind == JsonValueKind.Object ? ReadString(profileNode, "industry") : null;
            var website = profileNode.ValueKind == JsonValueKind.Object ? ReadString(profileNode, "website") : null;

            return (name, sector, industry, website);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no Yahoo quoteSummary para {Symbol}", yahooSymbol);
            return null;
        }
    }

    private async Task<MarketHistoryResponse?> TryGetYahooHistoryAsync(string symbol, string period, CancellationToken cancellationToken)
    {
        try
        {
            var yahooSymbol = ToYahooSymbol(symbol);
            var normalizedPeriod = NormalizePeriod(period);
            using var response = await _yahooClient.GetAsync($"v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}?interval=1d&range={normalizedPeriod}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yahoo history retornou status {Status} para {Symbol}", (int)response.StatusCode, symbol);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var result = json.RootElement.GetProperty("chart").GetProperty("result");
            if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return null;

            var first = result[0];
            var timestamps = first.GetProperty("timestamp").EnumerateArray().ToArray();
            var closes = first.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close").EnumerateArray().ToArray();
            var size = Math.Min(timestamps.Length, closes.Length);

            var points = new List<MarketHistoryPointResponse>(size);
            for (var i = 0; i < size; i++)
            {
                if (closes[i].ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
                if (!closes[i].TryGetDecimal(out var close) && closes[i].TryGetDouble(out var closeDouble))
                {
                    close = (decimal)closeDouble;
                }

                if (timestamps[i].ValueKind != JsonValueKind.Number || !timestamps[i].TryGetInt64(out var ts)) continue;

                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(ts).DateTime);
                points.Add(new MarketHistoryPointResponse(date, close));
            }

            return new MarketHistoryResponse(
                symbol,
                normalizedPeriod,
                "Yahoo chart",
                false,
                "Yahoo (free)",
                points);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no Yahoo history para {Symbol}", symbol);
            return null;
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Símbolo é obrigatório.", nameof(symbol));
        return symbol.Trim().ToUpperInvariant();
    }

    private static string ToYahooSymbol(string symbol)
    {
        if (symbol.Contains('.')) return symbol;
        return symbol.EndsWith(".SA", StringComparison.OrdinalIgnoreCase) ? symbol : $"{symbol}.SA";
    }

    private static string ToStooqSymbol(string symbol)
    {
        var raw = symbol.Replace(".SA", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        return $"{raw}.sa";
    }

    private static string NormalizePeriod(string period)
    {
        return period?.Trim().ToLowerInvariant() switch
        {
            "1mo" or "3mo" or "6mo" or "1y" or "2y" or "5y" => period.Trim().ToLowerInvariant(),
            _ => "6mo"
        };
    }

    private static string? BuildLogoUrl(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return null;
        if (!Uri.TryCreate(website, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host)) return null;
        return $"https://logo.clearbit.com/{host}";
    }

    private static decimal? ReadDecimal(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        if (value.TryGetDecimal(out var number)) return number;
        if (value.TryGetDouble(out var asDouble)) return (decimal)asDouble;
        return null;
    }

    private static long? ReadLong(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        return value.TryGetInt64(out var number) ? number : null;
    }

    private static string? ReadString(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }
}
