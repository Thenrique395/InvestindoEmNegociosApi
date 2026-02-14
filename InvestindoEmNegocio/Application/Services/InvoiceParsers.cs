using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Services;

public interface IInvoiceParser
{
    bool CanParse(string rawText, IReadOnlyList<string> lines);
    InvoiceExtractResponse Parse(string rawText, IReadOnlyList<string> lines);
}

public sealed class InvoiceParserFactory
{
    private readonly IReadOnlyList<IInvoiceParser> _parsers;

    public InvoiceParserFactory()
    {
        _parsers =
        [
            new SantanderInvoiceParser(),
            new ItauInvoiceParser(),
            new GenericInvoiceParser()
        ];
    }

    public InvoiceExtractResponse Parse(string rawText, IReadOnlyList<string> lines)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(rawText, lines))
            {
                return parser.Parse(rawText, lines);
            }
        }

        return new GenericInvoiceParser().Parse(rawText, lines);
    }
}

internal static class InvoiceParseCommon
{
    private static readonly Regex InstallmentRegex = new(@"\b(\d{2})/(\d{2})\b", RegexOptions.Compiled);
    public static readonly Regex DateRegex = new(@"(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled);
    public static readonly Regex ItemRegex = new(@"(\d{2}/\d{2})(?:/\d{2,4})?\s+(.+?)\s+R\$\s*([\d\.]+,\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static readonly Regex GenericItemRegex = new(@"(\d{2}/\d{2})\s*([A-Z0-9\*\.\-\/\s]{3,}?)\s(-?[\d\.]+,\d{2})(?=\s|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static readonly Regex DenseLineItemRegex = new(@"\d?(\d{2}/\d{2})\s*([A-Z0-9\*\.\-\/\s]{3,}?)(?:\s*\d{2}/\d{2,3})?\s*(-?[\d\.]+,\d{2})(?=(?:\s+\d?\d{2}/\d{2})|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static readonly Regex HolderLast4Regex = new(@"([A-Z\s]{5,})\s-\s\d{4}(?:\sX+){2,3}\s(\d{4})", RegexOptions.Compiled);

    public static string? FindMoneyByPattern(string rawText, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var match = regex.Match(rawText);
        if (!match.Success || match.Groups.Count < 2) return null;
        return $"R$ {match.Groups[1].Value}";
    }

    public static string? FindDateByLabel(IReadOnlyList<string> lines, IEnumerable<string> labels)
    {
        var labelPattern = string.Join('|', labels.Select(Regex.Escape));
        var regex = new Regex($"({labelPattern})", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            if (!regex.IsMatch(line)) continue;
            var match = DateRegex.Match(line);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    public static (string? cardHolder, string? cardLast4) FindHolderAndLast4(string rawText)
    {
        var match = HolderLast4Regex.Match(rawText.ToUpperInvariant());
        if (!match.Success) return (null, null);
        var holder = Regex.Replace(match.Groups[1].Value, "\\s+", " ").Trim();
        return (string.IsNullOrWhiteSpace(holder) ? null : holder, match.Groups[2].Value);
    }

    public static IReadOnlyList<InvoiceItemDto> ExtractItems(IReadOnlyList<string> lines, string rawText)
    {
        var items = new List<InvoiceItemDto>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = ItemRegex.Match(line);
            if (!match.Success) continue;
            AddItem(items, dedup, match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        }

        if (items.Count < 15)
        {
            foreach (Match match in GenericItemRegex.Matches(rawText))
            {
                AddItem(items, dedup, match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            }
        }

        if (items.Count < 25)
        {
            foreach (var line in lines)
            {
                foreach (Match match in DenseLineItemRegex.Matches(line))
                {
                    AddItem(items, dedup, match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
                }
            }
        }

        return items.Take(120).ToList();
    }

    private static void AddItem(List<InvoiceItemDto> items, HashSet<string> dedup, string date, string descriptionRaw, string amountRaw)
    {
        var description = SanitizeDescription(descriptionRaw);
        if (description.Length < 3) return;
        var amount = $"R$ {amountRaw}";
        var key = $"{date}|{description}|{amount}";
        if (!dedup.Add(key)) return;
        items.Add(CreateItem(date, description, amount));
    }

    private static string SanitizeDescription(string input)
    {
        var value = Regex.Replace(input, "\\s+", " ").Trim();
        return value.Length > 120 ? value[..120].Trim() : value;
    }

    public static InvoiceItemDto CreateItem(string? date, string description, string? amount, string? installmentToken = null)
    {
        var token = installmentToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            var tokenMatch = InstallmentRegex.Match(description);
            token = tokenMatch.Success ? tokenMatch.Value : null;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new InvoiceItemDto(date, description, amount);
        }

        var parsed = InstallmentRegex.Match(token);
        if (!parsed.Success) return new InvoiceItemDto(date, description, amount);

        var current = int.TryParse(parsed.Groups[1].Value, out var c) ? c : (int?)null;
        var total = int.TryParse(parsed.Groups[2].Value, out var t) ? t : (int?)null;
        var baseDescription = Regex.Replace(description, @"\s*\b\d{2}/\d{2}\b", string.Empty).Trim();

        return new InvoiceItemDto(
            date,
            description,
            amount,
            true,
            current,
            total,
            string.IsNullOrWhiteSpace(baseDescription) ? description : baseDescription
        );
    }
}

public sealed class SantanderInvoiceParser : IInvoiceParser
{
    private static readonly Regex CardNameRegex = new(@"cart[aã]o\s+([A-Z0-9\s]+?)\s+contendo", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CloseDateShortRegex = new(@"at[eé]\s+(\d{2}/\d{2})(?!/\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SantanderItemRegex = new(
        @"(?<!\d)\d?(\d{2}/\d{2})\s+([A-Z0-9À-Ú\*\.\-\/\s]{3,80}?)(?:\s+(\d{2}/\d{2}))?\s+(-?\d{1,3}(?:\.\d{3})*,\d{2})(?=(?:\s+\d?\d{2}/\d{2}|\s+VALOR\s+TOTAL|\s+RESUMO\s+DA\s+FATURA|\s+DETALHAMENTO\s+DA\s+FATURA|$))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool CanParse(string rawText, IReadOnlyList<string> lines)
    {
        return rawText.Contains("SANTANDER", StringComparison.OrdinalIgnoreCase);
    }

    public InvoiceExtractResponse Parse(string rawText, IReadOnlyList<string> lines)
    {
        var dueDate = InvoiceParseCommon.FindDateByLabel(lines, ["vencimento", "vencto"]);
        var closeDate = FindCloseDate(lines, rawText, dueDate);
        var cardName = FindCardName(rawText);
        var holderData = InvoiceParseCommon.FindHolderAndLast4(rawText);

        return new InvoiceExtractResponse(
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Total\s+a\s+Pagar\s*R\$\s*([\d\.]+,\d{2})")
                ?? InvoiceParseCommon.FindMoneyByPattern(rawText, @"Saldo\s+Desta\s+Fatura\s*([\d\.]+,\d{2})"),
            dueDate,
            closeDate,
            cardName,
            "Santander",
            ExtractSantanderItems(lines, rawText),
            rawText,
            holderData.cardHolder,
            holderData.cardLast4,
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Pagamento\s*M[ií]nimo\s*R\$\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Seu\s+lim\w*\s*[ée]?\s*R\$\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Limite\s+utilizado\s*R\$\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Limite\s+Dispon[ií]vel:\s*R\$\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Saldo\s+Anterior\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Total\s+Despesas\/D[eé]bitos\s+no\s+Brasil\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Total\s+de\s+pagamentos\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Total\s+de\s+cr[eé]ditos\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Saldo\s+Desta\s+Fatura\s*([\d\.]+,\d{2})")
        );
    }

    private static string? FindCardName(string rawText)
    {
        var match = CardNameRegex.Match(rawText);
        if (!match.Success) return null;
        var value = Regex.Replace(match.Groups[1].Value, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
        return match.Success ? ExpandShortDate(match.Groups[1].Value, dueDate) : null;
    }

    private static string ExpandShortDate(string shortDate, string? dueDate)
    {
        if (dueDate is not null && InvoiceParseCommon.DateRegex.IsMatch(dueDate))
        {
            return $"{shortDate}/{dueDate.Split('/').Last()}";
        }
        return shortDate;
    }

    private static IReadOnlyList<InvoiceItemDto> ExtractSantanderItems(IReadOnlyList<string> lines, string rawText)
    {
        var start = rawText.IndexOf("Detalhamento da Fatura", StringComparison.OrdinalIgnoreCase);
        var section = start >= 0 ? rawText[start..] : rawText;
        var normalized = NormalizeSantanderRawText(section);

        var items = new List<InvoiceItemDto>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SantanderItemRegex.Matches(normalized))
        {
            var date = match.Groups[1].Value;
            var description = Regex.Replace(match.Groups[2].Value, "\\s+", " ").Trim();
            var installment = match.Groups[3].Success ? match.Groups[3].Value : null;
            var amountRaw = match.Groups[4].Value.Replace(" ", string.Empty);

            if (description.Length < 3) continue;
            if (description.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("PAGAMENTO PERIODO", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("HISTORICO DE FATURAS", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("VALOR TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("RESUMO DA FATURA", StringComparison.OrdinalIgnoreCase)) continue;

            var amount = $"R$ {amountRaw}";
            var key = $"{date}|{description}|{amount}";
            if (!dedup.Add(key)) continue;
            items.Add(InvoiceParseCommon.CreateItem(date, description, amount, installment));
        }

        if (items.Count < 10)
        {
            return InvoiceParseCommon.ExtractItems(lines, rawText);
        }

        return items.Take(200).ToList();
    }

    private static string NormalizeSantanderRawText(string rawText)
    {
        var text = rawText.Replace('\n', ' ');
        text = Regex.Replace(text, @"(\d{2}/\d{2})(?=[A-Za-zÀ-Ú0-9@])", "$1 ");
        text = Regex.Replace(text, @"(\d{2}/\d{2})(?=\d{1,3}(?:\.\d{3})*,\d{2})", "$1 ");
        text = Regex.Replace(text, @"(\d{1,3}(?:\.\d{3})*,\d{2})(?=\d{1,2}/\d{2})", "$1 ");
        text = Regex.Replace(text, @"\s+", " ");
        return text;
    }
}

public sealed class ItauInvoiceParser : IInvoiceParser
{
    private static readonly Regex ItauCardLast4Regex = new(@"Cart[aã]o\s*\d{4}\.X{4}\.X{4}\.(\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ItauHolderRegex = new(@"Titular\s*([A-ZÀ-Ú\s]{5,}?)(?=Cart[aã]o|\d{4}\.X{4}\.X{4}\.\d{4}|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ItauItemRegex = new(
        @"(?<!\d)(\d{2}/\d{2})\s*([A-Za-z0-9À-Ú\*\.\-\/\s]{3,90}?)\s*(?:(\d{2}/\d{2})\s*)?(-?\d{1,3}(?:\.\d{3})*,\d{2})(?=(?:\s*(?:ALIMENTA|SA[UÚ]DE|VESTU|HOBBY|DIVERSOS|MORADIA|VE[ÍI]CULOS|TURISMO|EDUCA|DATAESTABELECIMENTO|Lançamentos|LANCAMENTOS|Total|Pr[oó]xima|Demais|Continua|JOANNA|TIAGO|\d{2}/\d{2}|$)))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ItauLooseItemRegex = new(
        @"(?<!\d)(\d{2}/\d{2})\s*([A-Za-z0-9À-Ú\*\.\-\/\s]{3,80}?)\s*(?:(\d{2}/\d{2})\s*)?(-?\d{1,3}(?:\.\d{3})*,\d{2})(?=(?:\s*\d{2}/\d{2}|\s*[A-ZÀ-Ú]{4,}|$))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool CanParse(string rawText, IReadOnlyList<string> lines)
    {
        return rawText.Contains("ITAU", StringComparison.OrdinalIgnoreCase)
               || rawText.Contains("ITAÚ", StringComparison.OrdinalIgnoreCase);
    }

    public InvoiceExtractResponse Parse(string rawText, IReadOnlyList<string> lines)
    {
        var dueDate = FindDate(rawText, @"Vencimento:\s*(\d{2}/\d{2}/\d{4})")
            ?? InvoiceParseCommon.FindDateByLabel(lines, ["vencimento", "vencto"]);
        var closeDate = FindDate(rawText, @"Emiss[aã]o:\s*(\d{2}/\d{2}/\d{4})")
            ?? FindDate(rawText, @"Previs[aã]o\s+prox\.?\s+Fechamento:\s*(\d{2}/\d{2}/\d{4})")
            ?? InvoiceParseCommon.FindDateByLabel(lines, ["fechamento", "emissão", "emissao"]);
        var holderData = FindHolderAndLast4(rawText);

        return new InvoiceExtractResponse(
            FindMoney(rawText, @"Total\s+desta\s+fatura\s*([\d\.]+,\d{2})")
                ?? FindMoney(rawText, @"O\s+total\s+da\s+sua\s+fatura\s+[ée]:?\s*R\$\s*([\d\.]+,\d{2})")
                ?? FindMoney(rawText, @"Total\s+da\s+fatura\s*R?\$?\s*([\d\.]+,\d{2})"),
            dueDate,
            closeDate,
            "Cartao Itau",
            "Itau",
            ExtractItauItems(lines, rawText),
            rawText,
            holderData.cardHolder,
            holderData.cardLast4,
            FindMoney(rawText, @"pagamento\s+m[ií]nimo[^\d\-]*(-?\s?[\d\.]+,\d{2})"),
            FindItauLimitTotal(rawText),
            FindMoney(rawText, @"Limite\s+total\s+utilizado\s*([\d\.]+,\d{2})"),
            FindMoney(rawText, @"Limite\s+dispon[ií]vel\s*([\d\.]+,\d{2})"),
            FindMoney(rawText, @"Total\s+da\s+fatura\s+anterior\s*([\d\.]+,\d{2})"),
            FindMoney(rawText, @"Total\s+dos\s+lan[cç]amentos\s+atuais\s*([\d\.]+,\d{2})"),
            FindMoney(rawText, @"Total\s+dos\s+pagamentos\s*(-?\s?[\d\.]+,\d{2})"),
            null,
            FindMoney(rawText, @"Total\s+desta\s+fatura\s*([\d\.]+,\d{2})")
        );
    }

    private static (string? cardHolder, string? cardLast4) FindHolderAndLast4(string rawText)
    {
        var holder = ItauHolderRegex.Match(rawText);
        var last4 = ItauCardLast4Regex.Match(rawText);
        var holderValue = holder.Success ? Regex.Replace(holder.Groups[1].Value, "\\s+", " ").Trim() : null;
        var last4Value = last4.Success ? last4.Groups[1].Value : null;
        return (string.IsNullOrWhiteSpace(holderValue) ? null : holderValue, last4Value);
    }

    private static string? FindDate(string rawText, string pattern)
    {
        var match = Regex.Match(rawText, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? FindMoney(string rawText, string pattern)
    {
        var match = Regex.Match(rawText, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        if (!match.Success) return null;
        return $"R$ {match.Groups[1].Value.Trim()}";
    }

    private static string? FindItauLimitTotal(string rawText)
    {
        var anchor = rawText.IndexOf("Limite total de crédito", StringComparison.OrdinalIgnoreCase);
        if (anchor < 0)
        {
            return FindMoney(rawText, @"Limite\s+total\s+de\s+cr[eé]dito:\s*R?\$?\s*([\d\.]+,\d{2})");
        }

        var windowSize = Math.Min(220, rawText.Length - anchor);
        var snippet = rawText.Substring(anchor, windowSize);
        var values = Regex.Matches(snippet, @"R\$\s*([\d\.]+,\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        if (values.Count == 0) return null;

        decimal max = -1m;
        string? best = null;
        foreach (Match m in values)
        {
            var value = m.Groups[1].Value;
            var normalized = value.Replace(".", string.Empty).Replace(',', '.');
            if (!decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }
            if (number > max)
            {
                max = number;
                best = value;
            }
        }
        return best is null ? null : $"R$ {best}";
    }

    private static string NormalizeItauRawText(string rawText)
    {
        var text = rawText.Replace('\n', ' ');
        text = Regex.Replace(text, @"(\d{2}/\d{2})(?=[A-Za-zÀ-Ú0-9])", "$1 ");
        text = Regex.Replace(text, @"(\d{1,3}(?:\.\d{3})*,\d{2})(?=[A-Za-zÀ-Ú])", "$1 ");
        text = Regex.Replace(text, @"(\d{1,3}(?:\.\d{3})*,\d{2})(?=\d{2}/\d{2})", "$1 ");
        text = Regex.Replace(text, @"\s+", " ");
        return text;
    }

    private static IReadOnlyList<InvoiceItemDto> ExtractItauItems(IReadOnlyList<string> lines, string rawText)
    {
        var normalizedRawText = NormalizeItauRawText(rawText);
        var items = new List<InvoiceItemDto>();
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ItauItemRegex.Matches(normalizedRawText))
        {
            var date = match.Groups[1].Value;
            var description = Regex.Replace(match.Groups[2].Value, "\\s+", " ").Trim();
            description = Regex.Replace(description, @"^\d{6,}(?=[A-Za-zÀ-Ú])", string.Empty).Trim();
            var installment = match.Groups[3].Success ? match.Groups[3].Value : null;
            var amountRaw = match.Groups[4].Value.Replace(" ", string.Empty);

            if (description.Length < 3) continue;
            if (description.Contains("PAGAMENTO EFETUADO", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("DESC ANTECIPA PARCELAS", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("LANCAMENTOS", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.Contains("DATAESTABELECIMENTO", StringComparison.OrdinalIgnoreCase)) continue;
            if (description.EndsWith("-", StringComparison.Ordinal)) continue;
            if (amountRaw.StartsWith("-", StringComparison.Ordinal)) continue;

            var amount = $"R$ {amountRaw}";
            var key = $"{date}|{description}|{amount}";
            if (!dedup.Add(key)) continue;
            items.Add(InvoiceParseCommon.CreateItem(date, description, amount, installment));
        }

        if (items.Count < 10)
        {
            foreach (Match match in ItauLooseItemRegex.Matches(normalizedRawText))
            {
                var date = match.Groups[1].Value;
                var description = Regex.Replace(match.Groups[2].Value, "\\s+", " ").Trim();
                description = Regex.Replace(description, @"^\d{6,}(?=[A-Za-zÀ-Ú])", string.Empty).Trim();
                var installment = match.Groups[3].Success ? match.Groups[3].Value : null;
                var amountRaw = match.Groups[4].Value.Replace(" ", string.Empty);

                if (description.Length < 3) continue;
                if (description.Contains("PAGAMENTO EFETUADO", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.Contains("DESC ANTECIPA PARCELAS", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.Contains("LANCAMENTOS", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.Contains("DATAESTABELECIMENTO", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.Contains("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.Contains("LIMITE", StringComparison.OrdinalIgnoreCase)) continue;
                if (description.EndsWith("-", StringComparison.Ordinal)) continue;
                if (amountRaw.StartsWith("-", StringComparison.Ordinal)) continue;

                var amount = $"R$ {amountRaw}";
                var key = $"{date}|{description}|{amount}";
                if (!dedup.Add(key)) continue;
                items.Add(InvoiceParseCommon.CreateItem(date, description, amount, installment));
            }
        }

        if (items.Count < 10)
        {
            return InvoiceParseCommon.ExtractItems(lines, rawText);
        }

        return items.Take(160).ToList();
    }
}

public sealed class GenericInvoiceParser : IInvoiceParser
{
    public bool CanParse(string rawText, IReadOnlyList<string> lines) => true;

    public InvoiceExtractResponse Parse(string rawText, IReadOnlyList<string> lines)
    {
        var holderData = InvoiceParseCommon.FindHolderAndLast4(rawText);
        return new InvoiceExtractResponse(
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Total\s+(?:a\s+pagar|da\s+fatura)\s*R\$\s*([\d\.]+,\d{2})"),
            InvoiceParseCommon.FindDateByLabel(lines, ["vencimento", "vencto"]),
            InvoiceParseCommon.FindDateByLabel(lines, ["fechamento", "até"]),
            null,
            DetectBank(rawText),
            InvoiceParseCommon.ExtractItems(lines, rawText),
            rawText,
            holderData.cardHolder,
            holderData.cardLast4,
            InvoiceParseCommon.FindMoneyByPattern(rawText, @"Pagamento\s+m[ií]nimo\s*R\$\s*([\d\.]+,\d{2})"),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }

    private static string? DetectBank(string rawText)
    {
        if (rawText.Contains("SANTANDER", StringComparison.OrdinalIgnoreCase)) return "Santander";
        if (rawText.Contains("BRADESCO", StringComparison.OrdinalIgnoreCase)) return "Bradesco";
        if (rawText.Contains("ITAU", StringComparison.OrdinalIgnoreCase) || rawText.Contains("ITAÚ", StringComparison.OrdinalIgnoreCase)) return "Itau";
        if (rawText.Contains("NUBANK", StringComparison.OrdinalIgnoreCase)) return "Nubank";
        return null;
    }
}
