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
    private static readonly Regex CardNameRegex = new(@"cart[aã]o\s+([A-Z0-9\s]+?)\s+contendo", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HolderLast4Regex = new(@"([A-Z\s]{5,})\s-\s\d{4}\sX+\sX+\sX+\s(\d{4})", RegexOptions.Compiled);
    private static readonly Regex CloseDateShortRegex = new(@"at[eé]\s+(\d{2}/\d{2})(?!/\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GenericItemRegex = new(@"(\d{2}/\d{2})\s*([A-Z0-9\*\.\-\/\s]{3,}?)\s(-?[\d\.]+,\d{2})(?=\s|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        var closeDate = FindCloseDate(normalized, rawText, dueDate)
                        ?? FindDateByLabel(normalized, ["fechamento", "data de fechamento", "fecha"]);
        var cardName = FindCardName(rawText) ?? FindByLabel(normalized, ["cartao", "cartão", "cartao de credito", "cartão de crédito"], true);
        var bankName = FindBankName(rawText) ?? FindByLabel(normalized, ["banco", "instituicao", "instituição"], true);
        var holderData = FindHolderAndLast4(rawText);
        var minimumPayment = FindMoneyByPattern(rawText, @"Pagamento\s*M[ií]nimo\s*R\$\s*([\d\.]+,\d{2})");
        var limitTotal = FindMoneyByPattern(rawText, @"Seu\s+lim\w*\s*[ée]?\s*R\$\s*([\d\.]+,\d{2})");
        var limitUsed = FindMoneyByPattern(rawText, @"Limite\s+utilizado\s*R\$\s*([\d\.]+,\d{2})");
        var limitAvailable = FindMoneyByPattern(rawText, @"Limite\s+Dispon[ií]vel:\s*R\$\s*([\d\.]+,\d{2})");
        var previousBalance = FindMoneyByPattern(rawText, @"Saldo\s+Anterior\s*([\d\.]+,\d{2})");
        var totalDebitsBrazil = FindMoneyByPattern(rawText, @"Total\s+Despesas\/D[eé]bitos\s+no\s+Brasil\s*([\d\.]+,\d{2})");
        var totalPayments = FindMoneyByPattern(rawText, @"Total\s+de\s+pagamentos\s*([\d\.]+,\d{2})");
        var totalCredits = FindMoneyByPattern(rawText, @"Total\s+de\s+cr[eé]ditos\s*([\d\.]+,\d{2})");
        var currentBalance = FindMoneyByPattern(rawText, @"Saldo\s+Desta\s+Fatura\s*([\d\.]+,\d{2})") ?? total;
        var items = ExtractItems(normalized, rawText);

        var response = new InvoiceExtractResponse(
            total,
            dueDate,
            closeDate,
            cardName,
            bankName,
            items,
            rawText,
            holderData.cardHolder,
            holderData.cardLast4,
            minimumPayment,
            limitTotal,
            limitUsed,
            limitAvailable,
            previousBalance,
            totalDebitsBrazil,
            totalPayments,
            totalCredits,
            currentBalance
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

    private static IReadOnlyList<InvoiceItemDto> ExtractItems(IReadOnlyList<string> lines, string rawText)
    {
        var items = new List<InvoiceItemDto>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var match = ItemRegex.Match(line);
            if (!match.Success) continue;
            var date = match.Groups[1].Value;
            var description = SanitizeDescription(match.Groups[2].Value);
            var amount = $"R$ {match.Groups[3].Value}";
            var key = $"{date}|{description}|{amount}";
            if (dedup.Add(key))
            {
                items.Add(new InvoiceItemDto(date, description, amount));
            }
        }
        if (items.Count < 5)
        {
            foreach (Match match in GenericItemRegex.Matches(rawText))
            {
                var date = match.Groups[1].Value;
                var description = SanitizeDescription(match.Groups[2].Value);
                if (description.Length < 3) continue;
                var amount = $"R$ {match.Groups[3].Value}";
                var key = $"{date}|{description}|{amount}";
                if (dedup.Add(key))
                {
                    items.Add(new InvoiceItemDto(date, description, amount));
                }
            }
        }
        return items.Take(120).ToList();
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

    private static string? FindCardName(string rawText)
    {
        var match = CardNameRegex.Match(rawText);
        if (!match.Success) return null;
        var value = Regex.Replace(match.Groups[1].Value, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? FindBankName(string rawText)
    {
        if (rawText.Contains("SANTANDER", StringComparison.OrdinalIgnoreCase)) return "Santander";
        if (rawText.Contains("BRADESCO", StringComparison.OrdinalIgnoreCase)) return "Bradesco";
        if (rawText.Contains("ITAU", StringComparison.OrdinalIgnoreCase)) return "Itau";
        if (rawText.Contains("NUBANK", StringComparison.OrdinalIgnoreCase)) return "Nubank";
        return null;
    }

    private static (string? cardHolder, string? cardLast4) FindHolderAndLast4(string rawText)
    {
        var match = HolderLast4Regex.Match(rawText.ToUpperInvariant());
        if (!match.Success) return (null, null);
        var holder = Regex.Replace(match.Groups[1].Value, "\\s+", " ").Trim();
        var last4 = match.Groups[2].Value;
        return (string.IsNullOrWhiteSpace(holder) ? null : holder, string.IsNullOrWhiteSpace(last4) ? null : last4);
    }

    private static string? FindMoneyByPattern(string rawText, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var match = regex.Match(rawText);
        if (!match.Success || match.Groups.Count < 2) return null;
        return $"R$ {match.Groups[1].Value}";
    }

    private static string? FindCloseDate(IReadOnlyList<string> lines, string rawText, string? dueDate)
    {
        foreach (var line in lines)
        {
            var m = CloseDateShortRegex.Match(line);
            if (m.Success)
            {
                return ExpandShortDate(m.Groups[1].Value, dueDate);
            }
        }

        var match = CloseDateShortRegex.Match(rawText);
        if (!match.Success) return null;
        return ExpandShortDate(match.Groups[1].Value, dueDate);
    }

    private static string ExpandShortDate(string shortDate, string? dueDate)
    {
        if (shortDate.Contains('/'))
        {
            if (dueDate is not null && DateRegex.IsMatch(dueDate))
            {
                var year = dueDate.Split('/').Last();
                return $"{shortDate}/{year}";
            }
        }
        return shortDate;
    }

    private static string SanitizeDescription(string input)
    {
        var value = Regex.Replace(input, "\\s+", " ").Trim();
        return value.Length > 120 ? value[..120].Trim() : value;
    }
}
