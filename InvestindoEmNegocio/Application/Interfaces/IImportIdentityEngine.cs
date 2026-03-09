using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IImportIdentityEngine
{
    Guid BuildLedgerSourceId(
        Guid accountId,
        Guid userId,
        DateTime occurredAt,
        decimal amount,
        AccountTransactionKind kind,
        string description,
        string? memo,
        string? externalId,
        string? type);

    string BuildInvoiceImportKey(
        string title,
        decimal amount,
        DateOnly dueDate,
        Guid? cardId);
}
