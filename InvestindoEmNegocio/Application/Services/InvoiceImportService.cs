using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using UglyToad.PdfPig;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvoiceImportService : IInvoiceImportService
{
    private static readonly Regex MoneyRegex = new(@"R\$\s*([\d\.]+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DateRegex = new(@"(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled);
    private static readonly Regex ItemRegex = new(@"(\d{2}/\d{2})(?:/\d{2,4})?\s+(.+?)\s+R\$\s*([\d\.]+,\d{2})", RegexOptions.Compiled);

    public Task<InvoiceExtractResponse> ExtractAsync(Stream pdfStream, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(pdfStream);
        var builder = new StringBuilder();
        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            builder.AppendLine(text);
            lines.AddRange(text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        var normalized = lines
            .Select(l => Regex.Replace(l, "\\s+", " ").Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var rawText = builder.ToString();

        var total = FindByLabel(normalized, ["total", "total da fatura", "valor total", "total a pagar"])
                    ?? FindLargestMoney(rawText);
        var dueDate = FindDateByLabel(normalized, ["vencimento", "data de vencimento", "vencto"]);
        var closeDate = FindDateByLabel(normalized, ["fechamento", "data de fechamento", "fecha"]);
        var cardName = FindByLabel(normalized, ["cartao", "cartão", "cartao de credito", "cartão de crédito"], true);
        var bankName = FindByLabel(normalized, ["banco", "instituicao", "instituição"], true);
        var items = ExtractItems(normalized);

        var response = new InvoiceExtractResponse(
            total,
            dueDate,
            closeDate,
            cardName,
            bankName,
            items,
            rawText
        );

        return Task.FromResult(response);
    }

    private static string? FindByLabel(IReadOnlyList<string> lines, IEnumerable<string> labels, bool loose = false)
    {
        var labelPattern = string.Join('|', labels.Select(Regex.Escape));
        var regex = new Regex($"({labelPattern})", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            if (!regex.IsMatch(line)) continue;
            var moneyMatch = MoneyRegex.Match(line);
            if (moneyMatch.Success) return $"R$ {moneyMatch.Groups[1].Value}";
            if (!loose) continue;
            var cleaned = regex.Replace(line, "").Replace(":", "").Replace("-", "").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
        return null;
    }

    private static string? FindDateByLabel(IReadOnlyList<string> lines, IEnumerable<string> labels)
    {
        var labelPattern = string.Join('|', labels.Select(Regex.Escape));
        var regex = new Regex($"({labelPattern})", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            if (!regex.IsMatch(line)) continue;
            var match = DateRegex.Match(line);
            if (match.Success) return match.Groups[1].Value;
        }
        foreach (var line in lines)
        {
            var match = DateRegex.Match(line);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    private static IReadOnlyList<InvoiceItemDto> ExtractItems(IReadOnlyList<string> lines)
    {
        var items = new List<InvoiceItemDto>();
        foreach (var line in lines)
        {
            var match = ItemRegex.Match(line);
            if (!match.Success) continue;
            items.Add(new InvoiceItemDto(
                match.Groups[1].Value,
                match.Groups[2].Value.Trim(),
                $"R$ {match.Groups[3].Value}"
            ));
        }
        return items.Take(60).ToList();
    }

    private static string? FindLargestMoney(string text)
    {
        var matches = MoneyRegex.Matches(text);
        if (matches.Count == 0) return null;

        decimal max = 0;
        string? maxText = null;
        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value;
            var value = ParseMoney(raw);
            if (value <= max) continue;
            max = value;
            maxText = raw;
        }

        return maxText is null ? null : $"R$ {maxText}";
    }

    private static decimal ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var normalized = value.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(normalized, out var result) ? result : 0;
    }
}
