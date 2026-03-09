using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ImportIdentityEngine : IImportIdentityEngine
{
    public Guid BuildLedgerSourceId(
        Guid accountId,
        Guid userId,
        DateTime occurredAt,
        decimal amount,
        AccountTransactionKind kind,
        string description,
        string? memo,
        string? externalId,
        string? type)
    {
        var stableKey = string.Join('|',
            accountId,
            userId,
            occurredAt.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            amount.ToString("F2", CultureInfo.InvariantCulture),
            kind,
            NormalizeKeyPart(description),
            NormalizeKeyPart(memo),
            NormalizeKeyPart(externalId),
            NormalizeKeyPart(type));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableKey));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    public string BuildInvoiceImportKey(string title, decimal amount, DateOnly dueDate, Guid? cardId)
        => $"{NormalizeKeyPart(title)}|{amount:F2}|{dueDate:yyyyMMdd}|{cardId?.ToString() ?? "NO_CARD"}";

    private static string NormalizeKeyPart(string? value)
        => Regex.Replace((value ?? string.Empty).Trim(), "\\s+", " ").ToUpperInvariant();
}
