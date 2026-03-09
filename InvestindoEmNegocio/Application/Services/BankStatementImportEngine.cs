using System.Globalization;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BankStatementImportEngine(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    IImportIdentityEngine importIdentityEngine,
    ICategorizationService categorizationService,
    IRecurrenceDetectorService recurrenceDetectorService,
    ILogger<BankStatementImportEngine> logger) : IBankStatementImportEngine
{
    private const string SourceType = "BankStatementImport";

    public async Task<IReadOnlyList<BankStatementPreviewItemDto>> BuildPreviewAsync(
        Guid userId,
        Guid? accountId,
        IReadOnlyList<BankStatementImportItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return [];

        var duplicateIds = await LoadExistingSourceIdsAsync(userId, accountId, items, cancellationToken);
        var preview = new List<BankStatementPreviewItemDto>(items.Count);
        foreach (var item in items)
        {
            var sourceId = accountId.HasValue
                ? BuildSourceId(accountId.Value, userId, item)
                : Guid.Empty;
            var suggestion = item.Kind == Domain.Enums.AccountTransactionKind.Debit
                ? await categorizationService.SuggestAsync(userId, Domain.Enums.MoneyType.Expense, item.Description, cancellationToken)
                : await categorizationService.SuggestAsync(userId, Domain.Enums.MoneyType.Income, item.Description, cancellationToken);
            var occurredAt = ParseOccurredAt(item.PostedAt);
            var recurrence = item.Kind == Domain.Enums.AccountTransactionKind.Debit
                ? await recurrenceDetectorService.SuggestAsync(userId, Domain.Enums.MoneyType.Expense, item.Description, item.Amount, occurredAt, cancellationToken)
                : await recurrenceDetectorService.SuggestAsync(userId, Domain.Enums.MoneyType.Income, item.Description, item.Amount, occurredAt, cancellationToken);
            preview.Add(new BankStatementPreviewItemDto(
                item.PostedAt,
                item.Amount,
                item.Kind,
                item.Description,
                item.Memo,
                item.ExternalId,
                item.Type,
                accountId.HasValue && duplicateIds.Contains(sourceId),
                suggestion,
                recurrence));
        }

        return preview;
    }

    public async Task<BankStatementImportResultResponse> ImportAsync(
        Guid userId,
        BankStatementImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Nenhuma movimentação foi enviada para importação.");

        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, cancellationToken);
        if (account is null)
            throw new InvalidOperationException("Conta selecionada não encontrada.");
        if (!account.IsActive)
            throw new InvalidOperationException("Conta selecionada está inativa.");

        var existingSourceIds = await LoadExistingSourceIdsAsync(userId, account.Id, request.Items, cancellationToken);
        var created = 0;
        var skipped = 0;

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = NormalizeItem(item);
            var sourceId = BuildSourceId(account.Id, userId, normalized);
            if (request.SkipDuplicates && existingSourceIds.Contains(sourceId))
            {
                skipped++;
                continue;
            }

            var occurredAt = ParseOccurredAt(normalized.PostedAt);
            await accountTransactionRepository.AddAsync(new AccountTransaction(
                account.Id,
                userId,
                occurredAt,
                normalized.Kind,
                normalized.Amount,
                normalized.Description,
                SourceType,
                sourceId), cancellationToken);

            if (normalized.CategoryId.HasValue)
            {
                var moneyType = normalized.Kind == Domain.Enums.AccountTransactionKind.Debit
                    ? Domain.Enums.MoneyType.Expense
                    : Domain.Enums.MoneyType.Income;
                await categorizationService.LearnAsync(
                    userId,
                    moneyType,
                    normalized.Description,
                    normalized.CategoryId.Value,
                    cancellationToken);
            }

            existingSourceIds.Add(sourceId);
            created++;
        }

        await accountTransactionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Importacao de extrato concluida para {UserId}. AccountId={AccountId} Created={Created} Skipped={Skipped}",
            userId,
            account.Id,
            created,
            skipped);

        return new BankStatementImportResultResponse(created, skipped);
    }

    private async Task<HashSet<Guid>> LoadExistingSourceIdsAsync(
        Guid userId,
        Guid? accountId,
        IReadOnlyList<BankStatementImportItemDto> items,
        CancellationToken cancellationToken)
    {
        if (!accountId.HasValue || items.Count == 0)
            return [];

        var account = await accountRepository.GetByIdAsync(accountId.Value, userId, cancellationToken);
        if (account is null)
            throw new InvalidOperationException("Conta selecionada não encontrada.");

        var sourceIds = items
            .Select(item => BuildSourceId(account.Id, userId, NormalizeItem(item)))
            .Distinct()
            .ToList();

        var existing = await accountTransactionRepository.ListBySourceAsync(
            userId,
            SourceType,
            sourceIds,
            cancellationToken) ?? [];

        return existing
            .Where(x => x.SourceId.HasValue)
            .Select(x => x.SourceId!.Value)
            .ToHashSet();
    }

    private BankStatementImportItemDto NormalizeItem(BankStatementImportItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.PostedAt))
            throw new ArgumentException("Data da movimentação é obrigatória.");
        if (item.Amount <= 0)
            throw new ArgumentException("Valor da movimentação deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(item.Description))
            throw new ArgumentException("Descrição da movimentação é obrigatória.");

        return item with
        {
            Description = CollapseSpaces(item.Description),
            Memo = string.IsNullOrWhiteSpace(item.Memo) ? null : CollapseSpaces(item.Memo),
            ExternalId = string.IsNullOrWhiteSpace(item.ExternalId) ? null : item.ExternalId.Trim(),
            Type = string.IsNullOrWhiteSpace(item.Type) ? null : item.Type.Trim()
        };
    }

    private Guid BuildSourceId(Guid accountId, Guid userId, BankStatementImportItemDto item)
        => importIdentityEngine.BuildLedgerSourceId(
            accountId,
            userId,
            ParseOccurredAt(item.PostedAt),
            item.Amount,
            item.Kind,
            item.Description,
            item.Memo,
            item.ExternalId,
            item.Type);

    private static DateTime ParseOccurredAt(string postedAt)
    {
        if (DateTime.TryParse(postedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
            return value;
        throw new ArgumentException("Data da movimentação inválida.");
    }

    private static string CollapseSpaces(string value)
        => System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\\s+", " ");
}
