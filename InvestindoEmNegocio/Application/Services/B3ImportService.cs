using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UglyToad.PdfPig;

namespace InvestindoEmNegocio.Application.Services;

public sealed class B3ImportService(
    InvestDbContext dbContext,
    IMemoryCache memoryCache,
    ILogger<B3ImportService> logger) : IB3ImportService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    public Task<B3ExtractResponse> ExtractAsync(Guid userId, Stream pdfStream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(pdfStream);
        var rawBuilder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            rawBuilder.AppendLine(page.Text);
        }

        var rawText = rawBuilder.ToString();
        var snapshot = B3ReportParser.Parse(rawText);
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .TrimEnd('=');

        memoryCache.Set(token, new CachedB3Snapshot(userId, snapshot), CacheTtl);

        return Task.FromResult(new B3ExtractResponse(
            token,
            snapshot.ReferenceMonth,
            snapshot.HolderName,
            snapshot.Document,
            new B3ExtractTotals(
                snapshot.Positions.Sum(x => x.UpdatedValue),
                snapshot.Incomes.Sum(x => x.NetValue),
                snapshot.Trades.Count),
            snapshot.Positions,
            snapshot.Incomes,
            snapshot.Trades,
            rawText));
    }

    public async Task<B3ConfirmImportResponse> ConfirmAsync(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImportToken))
        {
            throw new ArgumentException("Token de importação inválido.");
        }

        if (!memoryCache.TryGetValue<CachedB3Snapshot>(request.ImportToken, out var cached) || cached is null)
        {
            throw new ArgumentException("Token expirado. Faça a extração novamente.");
        }

        if (cached.UserId != userId)
        {
            throw new UnauthorizedAccessException("Importação pertence a outro usuário.");
        }

        var response = await ImportSnapshotAsync(
            userId,
            new B3ImportSnapshot(
                cached.Snapshot.ReferenceMonth,
                cached.Snapshot.HolderName,
                cached.Snapshot.Document,
                cached.Snapshot.Positions,
                cached.Snapshot.Incomes,
                cached.Snapshot.Trades),
            request.Strategy,
            cancellationToken);

        memoryCache.Remove(request.ImportToken);
        return response;
    }

    public async Task<B3ConfirmImportResponse> ImportSnapshotAsync(Guid userId, B3ImportSnapshot snapshot, string strategy, CancellationToken cancellationToken)
    {
        var importStrategy = ParseStrategy(strategy);
        var imported = 0;

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var userPositions = await dbContext.InvestmentPositions
            .Include(x => x.Movements)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (importStrategy == ImportStrategy.Replace)
        {
            var importedKeys = snapshot.Positions
                .Select(x => BuildPositionKey(NormalizeAssetCode(x.Product), x.Institution))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toRemove = userPositions
                .Where(x => importedKeys.Contains(BuildPositionKey(x.Asset, x.Account)))
                .ToList();

            if (toRemove.Count > 0)
            {
                dbContext.InvestmentPositions.RemoveRange(toRemove);
                await dbContext.SaveChangesAsync(cancellationToken);
                userPositions = userPositions.Except(toRemove).ToList();
            }
        }

        foreach (var parsedPosition in snapshot.Positions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assetCode = NormalizeAssetCode(parsedPosition.Product);
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                continue;
            }

            var key = BuildPositionKey(assetCode, parsedPosition.Institution);
            var existing = userPositions.FirstOrDefault(x =>
                string.Equals(BuildPositionKey(x.Asset, x.Account), key, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                var created = new InvestmentPosition(
                    userId,
                    ResolveInvestmentType(assetCode),
                    assetCode,
                    parsedPosition.Quantity,
                    parsedPosition.ClosingPrice,
                    ResolveOpenedAt(snapshot.ReferenceMonth),
                    parsedPosition.Institution,
                    "B3",
                    $"Importado da B3 ({snapshot.ReferenceMonth ?? "sem período"})");

                await dbContext.InvestmentPositions.AddAsync(created, cancellationToken);
                userPositions.Add(created);
                imported++;
                continue;
            }

            existing.Update(
                existing.Type,
                existing.Asset,
                parsedPosition.Quantity,
                parsedPosition.ClosingPrice,
                existing.OpenedAt,
                string.IsNullOrWhiteSpace(existing.Account) ? parsedPosition.Institution : existing.Account,
                string.IsNullOrWhiteSpace(existing.Category) ? "B3" : existing.Category,
                existing.Note);
            imported++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var trade in snapshot.Trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assetCode = NormalizeAssetCode(trade.Code);
            var position = FindPosition(userPositions, assetCode, trade.Institution);
            if (position is null)
            {
                continue;
            }

            if (trade.BuyQuantity > 0 && trade.AvgBuyPrice > 0)
            {
                imported += AddMovementIfNotExists(position, InvestmentMovementType.COMPRA, trade.BuyQuantity, trade.AvgBuyPrice, trade.Period, "Compra importada B3");
            }

            if (trade.SellQuantity > 0 && trade.AvgSellPrice > 0)
            {
                imported += AddMovementIfNotExists(position, InvestmentMovementType.VENDA, trade.SellQuantity, trade.AvgSellPrice, trade.Period, "Venda importada B3");
            }
        }

        foreach (var income in snapshot.Incomes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assetCode = NormalizeAssetCode(income.Product);
            var position = FindPosition(userPositions, assetCode, income.Institution);
            if (position is null)
            {
                continue;
            }

            var movementType = ResolveIncomeMovementType(income.EventType);
            var price = income.UnitPrice > 0 ? income.UnitPrice : 0.01m;
            var quantity = income.Quantity > 0 ? income.Quantity : 1m;
            imported += AddMovementIfNotExists(position, movementType, quantity, price, income.PaymentDate, $"Provento B3: {income.EventType}");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        logger.LogInformation("Importacao B3 concluida para usuario {UserId}. Itens importados: {Imported}", userId, imported);

        return new B3ConfirmImportResponse(imported);
    }

    private int AddMovementIfNotExists(
        InvestmentPosition position,
        InvestmentMovementType type,
        decimal quantity,
        decimal price,
        string dateRaw,
        string note)
    {
        if (!TryParseDate(dateRaw, out var date))
        {
            return 0;
        }

        var exists = position.Movements.Any(x =>
            x.Type == type &&
            x.Date == date &&
            x.Quantity == quantity &&
            x.Price == price &&
            string.Equals(x.Note, note, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return 0;
        }

        var movement = new InvestmentMovement(position.Id, type, quantity, price, date, note);
        position.ApplyMovement(movement);
        dbContext.InvestmentMovements.Add(movement);
        return 1;
    }

    private static InvestmentPosition? FindPosition(IEnumerable<InvestmentPosition> positions, string assetCode, string institution)
    {
        var normalizedInstitution = NormalizeInstitution(institution);
        return positions.FirstOrDefault(x =>
            string.Equals(x.Asset, assetCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeInstitution(x.Account), normalizedInstitution, StringComparison.OrdinalIgnoreCase))
            ?? positions.FirstOrDefault(x => string.Equals(x.Asset, assetCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPositionKey(string asset, string institution) =>
        $"{asset.Trim().ToUpperInvariant()}|{NormalizeInstitution(institution)}";

    private static string NormalizeInstitution(string institution) =>
        Regex.Replace(institution ?? string.Empty, "\\s+", " ").Trim().ToUpperInvariant();

    private static DateOnly ResolveOpenedAt(string? referenceMonth)
    {
        if (!string.IsNullOrWhiteSpace(referenceMonth) &&
            DateTime.TryParseExact(referenceMonth, "MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return new DateOnly(dt.Year, dt.Month, 1);
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        if (DateOnly.TryParseExact(raw, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        date = default;
        return false;
    }

    private static InvestmentType ResolveInvestmentType(string assetCode)
    {
        if (assetCode.Contains("11", StringComparison.OrdinalIgnoreCase))
        {
            return InvestmentType.FUNDOS;
        }

        if (Regex.IsMatch(assetCode, @"^[A-Z]{4}\d{1,2}$"))
        {
            return InvestmentType.ACOES;
        }

        return InvestmentType.RF;
    }

    private static InvestmentMovementType ResolveIncomeMovementType(string eventType)
    {
        var normalized = RemoveDiacritics(eventType).ToUpperInvariant();
        if (normalized.Contains("JUROS SOBRE CAPITAL PROPRIO", StringComparison.Ordinal))
        {
            return InvestmentMovementType.JCP;
        }

        if (normalized.Contains("DIVIDENDO", StringComparison.Ordinal))
        {
            return InvestmentMovementType.DIVIDENDO;
        }

        if (normalized.Contains("RENDIMENTO", StringComparison.Ordinal))
        {
            return InvestmentMovementType.RENDIMENTO;
        }

        return InvestmentMovementType.RENDIMENTO;
    }

    private static string NormalizeAssetCode(string product)
    {
        var value = Regex.Replace(product ?? string.Empty, "\\s+", " ").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var code = value.Contains(" - ", StringComparison.Ordinal)
            ? value[..value.IndexOf(" - ", StringComparison.Ordinal)].Trim()
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

        if (code.EndsWith('L') || code.EndsWith('F'))
        {
            code = code[..^1];
        }

        return code;
    }

    private static string RemoveDiacritics(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(NormalizationForm.FormC);
    }

    private static ImportStrategy ParseStrategy(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "replace" => ImportStrategy.Replace,
            _ => ImportStrategy.Merge
        };
    }

    private enum ImportStrategy
    {
        Merge,
        Replace
    }

    private sealed record CachedB3Snapshot(Guid UserId, B3ParsedSnapshot Snapshot);
}

internal sealed record B3ParsedSnapshot(
    string? ReferenceMonth,
    string? HolderName,
    string? Document,
    List<B3ExtractPosition> Positions,
    List<B3ExtractIncome> Incomes,
    List<B3ExtractTrade> Trades);

internal static class B3ReportParser
{
    private static readonly Regex HeaderRegex = new(
        @"(?<holder>[A-ZÀ-Ú\s]+)\s*\|\s*CPF\/CNPJ:\s*(?<doc>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ReferenceMonthRegex = new(
        @"Data:\s*(?<month>\d{2}\/\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PositionRegex = new(
        @"(?<product>[A-Z0-9]{4,8}\s*-\s*.+?)(?<type>ON|PN|UNT|COTAS?|Cotas)\s*(?<institution>BANCO\s*[A-Z\s\.]+S(?:\/A|\.A\.))\s*(?<quantity>\d+(?:,\d+)?)\s*R\$\s*(?<closing>-?[\d\.]+,\d{2})\s*R\$\s*(?<updated>-?[\d\.]+,\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex IncomeRegex = new(
        @"(?<product>[A-Z0-9]{4,8}[A-Z]?\s*-\s*.+?)(?<payment>\d{2}\/\d{2}\/\d{4})\s*(?<event>Juros\s*Sobre\s*Capital\s*Próprio|Juros\s*Sobre\s*Capital\s*Proprio|Dividendo|Rendimento)\s*(?<institution>BANCO\s*[A-Z\s\.]+S(?:\/A|\.A\.))\s*(?<quantity>\d+(?:,\d+)?)\s*R\$\s*(?<unit>-?[\d\.]+,\d{2})\s*R\$\s*(?<net>-?[\d\.]+,\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TradeRegex = new(
        @"(?<code>[A-Z0-9]{4,10})\s*(?<period>\d{2}\/\d{2}\/\d{4})\s*(?<institution>BANCO\s*[A-Z\s\.]+S(?:\/A|\.A\.))\s*(?<buy>\d+(?:,\d+)?)\s*(?<sell>\d+(?:,\d+)?)\s*(?<net>-?\d+(?:,\d+)?)\s*R\$\s*(?<avgBuy>-?[\d\.]+,\d{2})\s*R\$\s*(?<avgSell>-?[\d\.]+,\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AnnualIncomeRegex = new(
        @"(?<product>[A-Z0-9]{4,8}[A-Z]?)\s*(?<event>Juros\s*Sobre\s*Capital\s*Próprio|Juros\s*Sobre\s*Capital\s*Proprio|Dividendo|Rendimento)\s*R\$\s*(?<net>[\d\.]+,\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CompactTradeRegex = new(
        @"(?<code>[A-Z0-9]{4,10})(?<period>\d{2}\/\d{2}\/\d{4})(?<institution>BANCO\s+[A-Z\s]+S\/A)(?<qblock>\d+)(?:R\$\s*)?(?<avgBuy>[\d\.]+,\d{2})(?:R\$\s*)?(?<avgSell>[\d\.]+,\d{2})(?=(?:[A-Z0-9]{4,10}\d{2}\/\d{2}\/\d{4})|(?:Relatório)|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static B3ParsedSnapshot Parse(string rawText)
    {
        var headerMatch = HeaderRegex.Match(rawText);
        var referenceMatch = ReferenceMonthRegex.Match(rawText);
        var normalized = Regex.Replace(rawText, "\\s+", " ").Trim();

        var positionsSection = GetSection(normalized, "Posição - Ações", "Proventos recebidos");
        var incomesSection = GetSection(normalized, "Proventos recebidos", "Reembolsos de empréstimos de ativos", "Negociação");
        var tradesSection = GetSection(normalized, "Resumo dos negócios no período", "Relatório mensal consolidado");

        var positions = ParsePositions(positionsSection);
        var incomes = ParseIncomes(incomesSection);
        var trades = ParseTrades(tradesSection);

        if (positions.Count == 0)
        {
            positions = ParsePositions(normalized);
        }
        if (positions.Count == 0)
        {
            positions = ParsePositions(rawText);
        }

        if (incomes.Count == 0)
        {
            incomes = ParseIncomes(normalized);
        }
        if (incomes.Count == 0)
        {
            incomes = ParseIncomes(rawText);
        }
        if (incomes.Count == 0)
        {
            incomes = ParseAnnualIncomes(normalized);
        }

        if (trades.Count == 0)
        {
            trades = ParseTrades(normalized);
        }
        if (trades.Count == 0)
        {
            trades = ParseTrades(rawText);
        }
        if (trades.Count == 0)
        {
            trades = ParseCompactTrades(normalized);
        }

        return new B3ParsedSnapshot(
            referenceMatch.Success ? referenceMatch.Groups["month"].Value : null,
            headerMatch.Success ? NormalizeSpaces(headerMatch.Groups["holder"].Value) : null,
            headerMatch.Success ? headerMatch.Groups["doc"].Value : null,
            positions,
            incomes,
            trades);
    }

    private static string GetSection(string raw, string startMarker, params string[] endMarkers)
    {
        var start = raw.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return raw;
        }

        var end = raw.Length;
        foreach (var marker in endMarkers)
        {
            var markerIndex = raw.IndexOf(marker, start + startMarker.Length, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > start && markerIndex < end)
            {
                end = markerIndex;
            }
        }

        return raw[start..end];
    }

    private static List<B3ExtractPosition> ParsePositions(string section)
    {
        var rows = new List<B3ExtractPosition>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PositionRegex.Matches(section))
        {
            var product = NormalizeSpaces(match.Groups["product"].Value);
            var key = $"{product}|{match.Groups["institution"].Value}|{match.Groups["quantity"].Value}";
            if (!dedup.Add(key))
            {
                continue;
            }

            rows.Add(new B3ExtractPosition(
                product,
                NormalizeSpaces(match.Groups["type"].Value),
                NormalizeSpaces(match.Groups["institution"].Value),
                ParseDecimal(match.Groups["quantity"].Value),
                ParseMoney(match.Groups["closing"].Value),
                ParseMoney(match.Groups["updated"].Value)));
        }

        return rows;
    }

    private static List<B3ExtractIncome> ParseIncomes(string section)
    {
        var rows = new List<B3ExtractIncome>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in IncomeRegex.Matches(section))
        {
            var product = NormalizeSpaces(match.Groups["product"].Value);
            var paymentDate = match.Groups["payment"].Value;
            var eventType = NormalizeSpaces(match.Groups["event"].Value);
            var key = $"{product}|{paymentDate}|{eventType}|{match.Groups["net"].Value}";
            if (!dedup.Add(key))
            {
                continue;
            }

            rows.Add(new B3ExtractIncome(
                product,
                paymentDate,
                eventType,
                NormalizeSpaces(match.Groups["institution"].Value),
                ParseDecimal(match.Groups["quantity"].Value),
                ParseMoney(match.Groups["unit"].Value),
                ParseMoney(match.Groups["net"].Value)));
        }

        return rows;
    }

    private static List<B3ExtractTrade> ParseTrades(string section)
    {
        var rows = new List<B3ExtractTrade>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TradeRegex.Matches(section))
        {
            var code = NormalizeSpaces(match.Groups["code"].Value);
            var period = match.Groups["period"].Value;
            var key = $"{code}|{period}|{match.Groups["buy"].Value}|{match.Groups["sell"].Value}";
            if (!dedup.Add(key))
            {
                continue;
            }

            rows.Add(new B3ExtractTrade(
                code,
                period,
                NormalizeSpaces(match.Groups["institution"].Value),
                ParseDecimal(match.Groups["buy"].Value),
                ParseDecimal(match.Groups["sell"].Value),
                ParseDecimal(match.Groups["net"].Value),
                ParseMoney(match.Groups["avgBuy"].Value),
                ParseMoney(match.Groups["avgSell"].Value)));
        }

        return rows;
    }

    private static List<B3ExtractIncome> ParseAnnualIncomes(string raw)
    {
        var rows = new List<B3ExtractIncome>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AnnualIncomeRegex.Matches(raw))
        {
            var product = NormalizeSpaces(match.Groups["product"].Value);
            var eventType = NormalizeSpaces(match.Groups["event"].Value);
            var key = $"{product}|{eventType}|{match.Groups["net"].Value}";
            if (!dedup.Add(key))
            {
                continue;
            }

            rows.Add(new B3ExtractIncome(
                product,
                string.Empty,
                eventType,
                string.Empty,
                0,
                0,
                ParseMoney(match.Groups["net"].Value)));
        }

        return rows;
    }

    private static List<B3ExtractTrade> ParseCompactTrades(string raw)
    {
        var rows = new List<B3ExtractTrade>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CompactTradeRegex.Matches(raw))
        {
            var code = NormalizeSpaces(match.Groups["code"].Value);
            var period = match.Groups["period"].Value;
            var qblock = match.Groups["qblock"].Value;
            var (buy, sell, net) = ParseCompactQuantities(qblock);
            var key = $"{code}|{period}|{qblock}|{match.Groups["avgBuy"].Value}|{match.Groups["avgSell"].Value}";
            if (!dedup.Add(key))
            {
                continue;
            }

            rows.Add(new B3ExtractTrade(
                code,
                period,
                NormalizeSpaces(match.Groups["institution"].Value),
                buy,
                sell,
                net,
                ParseMoney(match.Groups["avgBuy"].Value),
                ParseMoney(match.Groups["avgSell"].Value)));
        }

        return rows;
    }

    private static (decimal buy, decimal sell, decimal net) ParseCompactQuantities(string qblock)
    {
        if (string.IsNullOrWhiteSpace(qblock))
        {
            return (0m, 0m, 0m);
        }

        if (qblock.Length == 3 && qblock.All(char.IsDigit))
        {
            return (qblock[0] - '0', qblock[1] - '0', qblock[2] - '0');
        }

        if (qblock.Length % 3 == 0)
        {
            var size = qblock.Length / 3;
            var p1 = qblock[..size];
            var p2 = qblock[size..(size * 2)];
            var p3 = qblock[(size * 2)..];
            return (ParseDecimal(p1), ParseDecimal(p2), ParseDecimal(p3));
        }

        return (0m, 0m, ParseDecimal(qblock));
    }

    private static decimal ParseMoney(string value)
    {
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        value = value.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(
            value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(
            value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

    private static string NormalizeSpaces(string input) =>
        Regex.Replace(input ?? string.Empty, "\\s+", " ").Trim();
}
