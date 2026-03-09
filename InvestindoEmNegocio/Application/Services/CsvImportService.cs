using System.Globalization;
using System.Text;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Utils;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

public sealed class CsvImportService(
    IBankStatementImportEngine bankStatementImportEngine) : ICsvImportService
{
    private static readonly string[] DateHeaders = ["data", "date", "dt", "postedat", "lancamento", "lançamento"];
    private static readonly string[] DescriptionHeaders = ["descricao", "descrição", "description", "historico", "histórico", "memo"];
    private static readonly string[] AmountHeaders = ["valor", "amount", "trnamt"];
    private static readonly string[] CreditHeaders = ["credito", "crédito", "credit", "entrada"];
    private static readonly string[] DebitHeaders = ["debito", "débito", "debit", "saida", "saída"];
    private static readonly string[] TypeHeaders = ["tipo", "type"];
    private static readonly string[] ExternalIdHeaders = ["id", "fitid", "documento", "doc"];

    public async Task<CsvExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rawText = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawText))
            throw new ArgumentException("Arquivo CSV vazio.");

        var lines = rawText
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (lines.Count == 0)
            throw new ArgumentException("Nenhuma linha CSV encontrada.");

        var delimiter = DetectDelimiter(lines);
        var rows = lines.Select(line => SplitCsvLine(line, delimiter)).ToList();
        var header = rows[0];
        var hasHeader = LooksLikeHeader(header);
        var dataRows = hasHeader ? rows.Skip(1).ToList() : rows;
        var columns = ResolveColumns(hasHeader ? header : null);

        var items = new List<BankStatementImportItemDto>();
        foreach (var row in dataRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = ParseRow(row, columns);
            if (item is not null)
                items.Add(item);
        }

        if (items.Count == 0)
            throw new ArgumentException("Nenhuma movimentação CSV válida encontrada.");

        var preview = await bankStatementImportEngine.BuildPreviewAsync(userId, accountId, items, cancellationToken);
        return new CsvExtractResponse(delimiter.ToString(), columns.DetectedColumns, preview, rawText);
    }

    public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken)
        => bankStatementImportEngine.ImportAsync(userId, request, cancellationToken);

    private static CsvColumnMap ResolveColumns(IReadOnlyList<string>? header)
    {
        if (header is null)
            return new CsvColumnMap(0, 1, 2, null, null, null, [], null);

        var normalized = header.Select(NormalizeHeader).ToList();
        return new CsvColumnMap(
            FindIndex(normalized, DateHeaders) ?? 0,
            FindIndex(normalized, DescriptionHeaders) ?? 1,
            FindIndex(normalized, AmountHeaders),
            FindIndex(normalized, CreditHeaders),
            FindIndex(normalized, DebitHeaders),
            FindIndex(normalized, TypeHeaders),
            normalized.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            FindIndex(normalized, ExternalIdHeaders));
    }

    private static BankStatementImportItemDto? ParseRow(IReadOnlyList<string> row, CsvColumnMap columns)
    {
        var postedAtRaw = GetCell(row, columns.DateIndex);
        var description = GetCell(row, columns.DescriptionIndex);
        if (string.IsNullOrWhiteSpace(postedAtRaw) || string.IsNullOrWhiteSpace(description))
            return null;

        var postedAt = ParseCsvDate(postedAtRaw);
        if (postedAt is null)
            return null;

        var creditValue = columns.CreditIndex.HasValue ? ParseMoney(GetCell(row, columns.CreditIndex.Value)) : null;
        var debitValue = columns.DebitIndex.HasValue ? ParseMoney(GetCell(row, columns.DebitIndex.Value)) : null;
        var amountValue = columns.AmountIndex.HasValue ? ParseMoney(GetCell(row, columns.AmountIndex.Value), allowSigned: true) : null;
        var typeValue = columns.TypeIndex.HasValue ? GetCell(row, columns.TypeIndex.Value) : null;

        decimal amount;
        AccountTransactionKind kind;
        if (creditValue.HasValue && creditValue.Value > 0)
        {
            amount = creditValue.Value;
            kind = AccountTransactionKind.Credit;
        }
        else if (debitValue.HasValue && debitValue.Value > 0)
        {
            amount = debitValue.Value;
            kind = AccountTransactionKind.Debit;
        }
        else if (amountValue.HasValue && amountValue.Value != 0)
        {
            kind = amountValue.Value < 0 ? AccountTransactionKind.Debit : ResolveKind(typeValue, amountValue.Value);
            amount = Math.Abs(amountValue.Value);
        }
        else
        {
            return null;
        }

        return new BankStatementImportItemDto(
            postedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            amount,
            kind,
            description.Trim(),
            null,
            columns.ExternalIdIndex.HasValue ? GetCell(row, columns.ExternalIdIndex.Value) : null,
            typeValue,
            null);
    }

    private static DateTime? ParseCsvDate(string raw)
    {
        var formats = new[]
        {
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ"
        };

        if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            return exact;
        if (DateTime.TryParse(raw.Trim(), CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback))
            return fallback;
        return null;
    }

    private static decimal? ParseMoney(string? raw, bool allowSigned = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!FinanceInputParser.TryParseMoney(raw, out var value))
        {
            var cleaned = raw.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim().Replace(",", ".");
            if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                return null;
        }
        return allowSigned ? value : Math.Abs(value);
    }

    private static AccountTransactionKind ResolveKind(string? typeValue, decimal amountValue)
    {
        if (amountValue < 0)
            return AccountTransactionKind.Debit;
        return NormalizeHeader(typeValue).Contains("debito") || NormalizeHeader(typeValue).Contains("debit")
            ? AccountTransactionKind.Debit
            : AccountTransactionKind.Credit;
    }

    private static char DetectDelimiter(IEnumerable<string> lines)
    {
        var candidates = new[] { ';', ',', '\t', '|' };
        return candidates
            .OrderByDescending(candidate => lines.Take(5).Sum(line => line.Count(ch => ch == candidate)))
            .First();
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> row)
        => row.Select(NormalizeHeader).Any(value =>
            DateHeaders.Contains(value) ||
            DescriptionHeaders.Contains(value) ||
            AmountHeaders.Contains(value) ||
            CreditHeaders.Contains(value) ||
            DebitHeaders.Contains(value));

    private static int? FindIndex(IReadOnlyList<string> values, IEnumerable<string> aliases)
    {
        var aliasSet = aliases.ToHashSet();
        for (var index = 0; index < values.Count; index++)
        {
            if (aliasSet.Contains(values[index]))
                return index;
        }
        return null;
    }

    private static string NormalizeHeader(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string? GetCell(IReadOnlyList<string> row, int index)
        => index >= 0 && index < row.Count ? row[index] : null;

    private sealed record CsvColumnMap(
        int DateIndex,
        int DescriptionIndex,
        int? AmountIndex,
        int? CreditIndex,
        int? DebitIndex,
        int? TypeIndex,
        IReadOnlyList<string> DetectedColumns,
        int? ExternalIdIndex);
}
