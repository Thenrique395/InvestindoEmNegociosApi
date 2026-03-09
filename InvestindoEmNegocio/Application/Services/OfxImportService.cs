using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

public sealed partial class OfxImportService(
    IBankStatementImportEngine bankStatementImportEngine) : IOfxImportService
{
    public async Task<OfxExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken)
    {
        var rawText = await ReadAllTextAsync(stream, cancellationToken);
        var document = ParseDocument(rawText);
        var preview = await bankStatementImportEngine.BuildPreviewAsync(userId, accountId, document.Transactions, cancellationToken);

        return new OfxExtractResponse(
            document.BankId,
            document.BranchId,
            document.AccountNumber,
            document.AccountType,
            document.StartDate?.ToString("yyyy-MM-dd"),
            document.EndDate?.ToString("yyyy-MM-dd"),
            document.LedgerBalance,
            preview,
            rawText);
    }

    public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken)
        => bankStatementImportEngine.ImportAsync(userId, request, cancellationToken);

    private static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Arquivo OFX vazio.");
        return raw;
    }

    private static OfxDocument ParseDocument(string rawText)
    {
        var normalized = rawText.Replace("\r\n", "\n");
        var transactions = StatementTransactionRegex()
            .Matches(normalized)
            .Select(match => ParseTransaction(match.Groups["content"].Value))
            .Where(x => x is not null)
            .Cast<BankStatementImportItemDto>()
            .ToList();

        if (transactions.Count == 0)
            throw new ArgumentException("Nenhuma movimentação OFX encontrada no arquivo.");

        return new OfxDocument(
            GetTagValue(normalized, "BANKID"),
            GetTagValue(normalized, "BRANCHID") ?? GetTagValue(normalized, "BRANCH"),
            GetTagValue(normalized, "ACCTID"),
            GetTagValue(normalized, "ACCTTYPE"),
            ParseOfxDate(GetTagValue(normalized, "DTSTART")),
            ParseOfxDate(GetTagValue(normalized, "DTEND")),
            ParseDecimal(GetTagValue(normalized, "BALAMT")),
            transactions);
    }

    private static BankStatementImportItemDto? ParseTransaction(string content)
    {
        var postedAt = ParseOfxDate(GetTagValue(content, "DTPOSTED"));
        var signedAmount = ParseSignedAmount(GetTagValue(content, "TRNAMT"));
        if (!postedAt.HasValue || !signedAmount.HasValue || signedAmount.Value == 0)
            return null;

        var type = GetTagValue(content, "TRNTYPE");
        var externalId = GetTagValue(content, "FITID");
        var memo = GetTagValue(content, "MEMO");
        var name = GetTagValue(content, "NAME");
        var checkNumber = GetTagValue(content, "CHECKNUM");
        var kind = ResolveKind(type, signedAmount.Value);
        var amount = Math.Abs(signedAmount.Value);
        var description = NormalizeDescription(name, memo, type, checkNumber);

        return new BankStatementImportItemDto(
            postedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            amount,
            kind,
            description,
            memo,
            externalId,
            type,
            null);
    }

    private static AccountTransactionKind ResolveKind(string? type, decimal signedAmount)
    {
        if (signedAmount < 0)
            return AccountTransactionKind.Debit;

        return type?.Trim().ToUpperInvariant() switch
        {
            "DEBIT" => AccountTransactionKind.Debit,
            "PAYMENT" => AccountTransactionKind.Debit,
            "POS" => AccountTransactionKind.Debit,
            "ATM" => AccountTransactionKind.Debit,
            _ => AccountTransactionKind.Credit
        };
    }

    private static string NormalizeDescription(string? name, string? memo, string? type, string? checkNumber = null)
    {
        var description = name;
        if (string.IsNullOrWhiteSpace(description))
            description = memo;
        if (string.IsNullOrWhiteSpace(description))
            description = checkNumber;
        if (string.IsNullOrWhiteSpace(description))
            description = type;
        if (string.IsNullOrWhiteSpace(description))
            description = "Movimentação OFX";

        return Regex.Replace(description.Trim(), "\\s+", " ");
    }

    private static decimal? ParseSignedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal? ParseDecimal(string? raw)
    {
        var value = ParseSignedAmount(raw);
        return value.HasValue ? Math.Abs(value.Value) : null;
    }

    private static DateTime? ParseOfxDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
            return null;

        var year = int.Parse(digits[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(digits.Substring(6, 2), CultureInfo.InvariantCulture);
        var hour = digits.Length >= 10 ? int.Parse(digits.Substring(8, 2), CultureInfo.InvariantCulture) : 0;
        var minute = digits.Length >= 12 ? int.Parse(digits.Substring(10, 2), CultureInfo.InvariantCulture) : 0;
        var second = digits.Length >= 14 ? int.Parse(digits.Substring(12, 2), CultureInfo.InvariantCulture) : 0;

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }

    private static string? GetTagValue(string text, string tag)
    {
        var match = Regex.Match(text, $@"(?is)<{tag}>\s*(?<value>[^<\n\r]+)");
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    [GeneratedRegex(@"(?is)<STMTTRN>\s*(?<content>.*?)\s*</STMTTRN>")]
    private static partial Regex StatementTransactionRegex();

    private sealed record OfxDocument(
        string? BankId,
        string? BranchId,
        string? AccountNumber,
        string? AccountType,
        DateTime? StartDate,
        DateTime? EndDate,
        decimal? LedgerBalance,
        IReadOnlyList<BankStatementImportItemDto> Transactions);
}
