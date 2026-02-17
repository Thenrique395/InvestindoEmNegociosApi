using System.Globalization;
using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvestmentBenchmarksService(
    HttpClient httpClient,
    ILogger<InvestmentBenchmarksService> logger) : IInvestmentBenchmarksService
{
    private const int SelicSeriesCode = 11;
    private const int IpcaSeriesCode = 433;

    public async Task<InvestmentBenchmarksResponse> GetBenchmarksAsync(int months, CancellationToken cancellationToken = default)
    {
        var safeMonths = Math.Clamp(months, 1, 36);
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(safeMonths - 1));
        var end = DateTime.UtcNow.Date;

        var selic = await GetAccumulatedSeriesAsync(SelicSeriesCode, start, end, cancellationToken);
        var ipca = await GetAccumulatedSeriesAsync(IpcaSeriesCode, start, end, cancellationToken);

        return new InvestmentBenchmarksResponse(
            safeMonths,
            [
                new InvestmentBenchmarkItemDto("SELIC (BCB)", selic, "BCB/SGS-11", false),
                new InvestmentBenchmarkItemDto("IPCA (BCB)", ipca, "BCB/SGS-433", false),
                new InvestmentBenchmarkItemDto("Ibovespa (estimado)", 5.8m, "estimado", true),
                new InvestmentBenchmarkItemDto("S&P500 (estimado)", 6.7m, "estimado", true)
            ]);
    }

    private async Task<decimal> GetAccumulatedSeriesAsync(int seriesCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var start = startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var end = endDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var path = $"dados/serie/bcdata.sgs.{seriesCode}/dados?formato=json&dataInicial={Uri.EscapeDataString(start)}&dataFinal={Uri.EscapeDataString(end)}";

        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("BCB retornou status {StatusCode} para série {SeriesCode}", (int)response.StatusCode, seriesCode);
                return 0m;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var points = await JsonSerializer.DeserializeAsync<List<BcbPoint>>(stream, cancellationToken: cancellationToken);
            if (points is null || points.Count == 0)
            {
                return 0m;
            }

            decimal factor = 1m;
            foreach (var point in points)
            {
                var rate = ParseRate(point.Valor);
                factor *= 1m + (rate / 100m);
            }

            return (factor - 1m) * 100m;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar série BCB {SeriesCode}", seriesCode);
            return 0m;
        }
    }

    private static decimal ParseRate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0m;
        }

        var normalized = raw.Contains(',')
            ? raw.Replace(".", "").Replace(",", ".")
            : raw;
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        return 0m;
    }

    private sealed record BcbPoint(string Data, string Valor);
}
