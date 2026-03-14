using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Utils;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvoiceImportService(
    InvoiceParserFactory parserFactory,
    IPlansService plansService,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository planRepository,
    ICardRepository cardRepository,
    IImportIdentityEngine importIdentityEngine,
    ICategorizationService categorizationService,
    IRecurrenceDetectorService recurrenceDetectorService,
    IInvestDbContext dbContext,
    ILogger<InvoiceImportService> logger) : IInvoiceImportService
{
    public async Task<InvoiceExtractResponse> ExtractAsync(Guid userId, Stream pdfStream, CancellationToken cancellationToken)
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
        var parsed = parserFactory.Parse(rawText, normalized);
        var suggestedItems = new List<InvoiceItemDto>(parsed.Items.Count);
        foreach (var item in parsed.Items)
        {
            var description = !string.IsNullOrWhiteSpace(item.BaseDescription)
                ? item.BaseDescription!
                : item.Description;
            var suggestion = await categorizationService.SuggestAsync(userId, MoneyType.Expense, description, cancellationToken);
            var parsedDate = FinanceInputParser.ParseDateOrDefault(item.Date, DateOnly.FromDateTime(DateTime.UtcNow));
            var parsedAmount = FinanceInputParser.TryParseMoney(item.Amount, out var amount) ? amount : 0m;
            var recurrence = parsedAmount > 0
                ? await recurrenceDetectorService.SuggestAsync(
                    userId,
                    MoneyType.Expense,
                    description,
                    parsedAmount,
                    parsedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    cancellationToken)
                : null;
            suggestedItems.Add(item with
            {
                SuggestedCategoryId = suggestion?.CategoryId,
                SuggestedCategoryName = suggestion?.CategoryName,
                SuggestedCategoryConfidence = suggestion?.Confidence,
                SuggestedCategoryScore = suggestion?.Score,
                SuggestedCategoryConfidenceBand = suggestion?.ConfidenceBand,
                SuggestedCategoryReasonCode = suggestion?.ReasonCode,
                SuggestedRecurrence = recurrence
            });
        }

        return parsed with { Items = suggestedItems };
    }

    public async Task<InvoiceImportResultResponse> ImportAsync(Guid userId, InvoiceImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Nenhum item de fatura foi enviado para importação.");

        Card? selectedCard = null;
        if (request.CardId.HasValue)
        {
            selectedCard = await cardRepository.GetByIdAsync(request.CardId.Value, userId, cancellationToken);
            if (selectedCard is null)
                throw new InvalidOperationException("Cartão selecionado não encontrado para o usuário.");
        }

        var defaultDate = FinanceInputParser.ParseDateOrDefault(request.DefaultDueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        var created = 0;
        var skipped = 0;
        var failed = 0;

        var dedupeKeys = request.SkipDuplicates
            ? await LoadExistingKeysAsync(userId, request.CardId, cancellationToken)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var requestIdempotency = string.IsNullOrWhiteSpace(request.ImportIdempotencyKey)
            ? BuildRequestIdempotencyKey(request)
            : request.ImportIdempotencyKey!.Trim();

        logger.LogInformation(
            "Iniciando importacao de fatura para {UserId}. Itens={Items}, CardId={CardId}, IdempotencyKey={IdempotencyKey}",
            userId,
            request.Items.Count,
            request.CardId,
            requestIdempotency);

        await using var tx = await dbContext.BeginTransactionAsync(cancellationToken);
        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = NormalizeTitle(item);
            if (string.IsNullOrWhiteSpace(title))
            {
                skipped++;
                continue;
            }

            if (!FinanceInputParser.TryParseMoney(item.Amount, out var amount) || amount <= 0m)
            {
                skipped++;
                continue;
            }

            var purchaseDate = FinanceInputParser.ParseDateOrDefault(item.Date, defaultDate);
            var dueDate = selectedCard is not null
                ? CardStatementCycleCalculator.Calculate(
                    purchaseDate,
                    selectedCard.StatementCloseDay,
                    selectedCard.DueDay).StatementDueDate
                : purchaseDate;

            var dedupeKey = importIdentityEngine.BuildInvoiceImportKey(title, amount, dueDate, request.CardId);
            if (request.SkipDuplicates && dedupeKeys.Contains(dedupeKey))
            {
                skipped++;
                continue;
            }

            var createRequest = new CreatePlanRequest(
                MoneyType.Expense,
                title,
                amount,
                ScheduleType.OneTime,
                purchaseDate,
                Frequency: null,
                InstallmentsCount: 1,
                DefaultPaymentMethodId: null,
                CategoryId: item.CategoryId ?? request.CategoryId,
                CardId: request.CardId);

            try
            {
                await plansService.CreateAsync(userId, createRequest, cancellationToken);
                if (createRequest.CategoryId.HasValue)
                {
                    await categorizationService.LearnAsync(
                        userId,
                        MoneyType.Expense,
                        title,
                        createRequest.CategoryId.Value,
                        cancellationToken);
                }
                dedupeKeys.Add(dedupeKey);
                created++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao importar item da fatura para {UserId}. Item={Description}", userId, item.Description);
                failed++;
            }
        }
        await tx.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Importacao de fatura finalizada para {UserId}. Created={Created}, Skipped={Skipped}, Failed={Failed}, IdempotencyKey={IdempotencyKey}",
            userId,
            created,
            skipped,
            failed,
            requestIdempotency);

        return new InvoiceImportResultResponse(created, skipped, failed);
    }

    public async Task<InvoiceReconciliationResponse> ReconcileAsync(Guid userId, InvoiceImportRequest request, CancellationToken cancellationToken)
    {
        if (!request.CardId.HasValue)
            throw new ArgumentException("Selecione um cartão para conciliar a fatura.");
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Nenhum item de fatura foi enviado para conciliação.");

        var card = await cardRepository.GetByIdAsync(request.CardId.Value, userId, cancellationToken);
        if (card is null)
            throw new InvalidOperationException("Cartão selecionado não encontrado para o usuário.");

        var plans = await planRepository.ListByUserAsync(userId, MoneyType.Expense, cancellationToken);
        var cardPlans = plans
            .Where(x => x.CardId == card.Id)
            .ToDictionary(x => x.Id, x => x.Title);
        var installments = await installmentRepository.ListByUserAsync(
            userId,
            null,
            null,
            null,
            MoneyType.Expense,
            cancellationToken);
        var cardInstallments = installments
            .Where(x => cardPlans.ContainsKey(x.PlanId))
            .Where(x => x.StatementYear.HasValue && x.StatementMonth.HasValue && x.StatementCloseDate.HasValue && x.StatementDueDate.HasValue)
            .ToList();

        var existingByKey = cardInstallments
            .GroupBy(x =>
            {
                var title = cardPlans.GetValueOrDefault(x.PlanId, "Despesa");
                return importIdentityEngine.BuildInvoiceImportKey(title, x.Amount, x.DueDate, card.Id);
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        decimal? parsedInvoiceTotal = FinanceInputParser.TryParseMoney(request.InvoiceTotal, out var totalAmount)
            ? totalAmount
            : null;
        var parsedCloseDate = string.IsNullOrWhiteSpace(request.StatementCloseDate)
            ? (DateOnly?)null
            : FinanceInputParser.ParseDateOrDefault(request.StatementCloseDate, DateOnly.FromDateTime(DateTime.UtcNow));
        var parsedDueDate = string.IsNullOrWhiteSpace(request.DefaultDueDate)
            ? (DateOnly?)null
            : FinanceInputParser.ParseDateOrDefault(request.DefaultDueDate, DateOnly.FromDateTime(DateTime.UtcNow));

        var items = new List<InvoiceReconciliationItemResponse>(request.Items.Count);
        var projectedByCycle = new Dictionary<string, (decimal importedNew, decimal duplicateAmount, int importedNewCount, int duplicateCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            var title = NormalizeTitle(item);
            if (string.IsNullOrWhiteSpace(title))
                continue;
            if (!FinanceInputParser.TryParseMoney(item.Amount, out var amount) || amount <= 0m)
                continue;

            var purchaseDate = FinanceInputParser.ParseDateOrDefault(item.Date, parsedDueDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
            var cycle = CardStatementCycleCalculator.Calculate(purchaseDate, card.StatementCloseDay, card.DueDay);
            var dedupeKey = importIdentityEngine.BuildInvoiceImportKey(title, amount, cycle.StatementDueDate, card.Id);
            var duplicate = existingByKey.TryGetValue(dedupeKey, out var existingInstallment);
            var statementReference = $"{cycle.StatementMonth:00}/{cycle.StatementYear:0000}";
            var cycleKey = $"{cycle.StatementYear}-{cycle.StatementMonth:00}";

            if (!projectedByCycle.ContainsKey(cycleKey))
                projectedByCycle[cycleKey] = (0m, 0m, 0, 0);

            var acc = projectedByCycle[cycleKey];
            if (duplicate)
                projectedByCycle[cycleKey] = (acc.importedNew, acc.duplicateAmount + amount, acc.importedNewCount, acc.duplicateCount + 1);
            else
                projectedByCycle[cycleKey] = (acc.importedNew + amount, acc.duplicateAmount, acc.importedNewCount + 1, acc.duplicateCount);

            items.Add(new InvoiceReconciliationItemResponse(
                title,
                item.BaseDescription,
                item.Date,
                amount,
                duplicate,
                duplicate ? "duplicate_statement_item" : "new_statement_item",
                cycle.StatementYear,
                cycle.StatementMonth,
                statementReference,
                cycle.StatementDueDate,
                existingInstallment?.Id));
        }

        var cycles = projectedByCycle
            .OrderByDescending(x => x.Key)
            .Select(entry =>
            {
                var year = int.Parse(entry.Key[..4]);
                var month = int.Parse(entry.Key[5..7]);
                var currentInstallments = cardInstallments
                    .Where(x => x.StatementYear == year && x.StatementMonth == month)
                    .ToList();
                var currentTotal = currentInstallments.Sum(x => x.Amount);
                var closeDate = currentInstallments.FirstOrDefault()?.StatementCloseDate
                    ?? parsedCloseDate
                    ?? BuildDate(year, month, card.StatementCloseDay);
                var dueDate = currentInstallments.FirstOrDefault()?.StatementDueDate
                    ?? parsedDueDate
                    ?? CardStatementCycleCalculator.Calculate(closeDate.AddMonths(-1), card.StatementCloseDay, card.DueDay).StatementDueDate;
                var projectedTotal = currentTotal + entry.Value.importedNew;
                var referencesParsedCycle = (!parsedCloseDate.HasValue || parsedCloseDate.Value == closeDate)
                    && (!parsedDueDate.HasValue || parsedDueDate.Value == dueDate);
                decimal? difference = parsedInvoiceTotal.HasValue && referencesParsedCycle
                    ? projectedTotal - parsedInvoiceTotal.Value
                    : null;

                return new InvoiceReconciliationCycleResponse(
                    year,
                    month,
                    closeDate,
                    dueDate,
                    $"{month:00}/{year:0000}",
                    currentTotal,
                    entry.Value.importedNew,
                    entry.Value.duplicateAmount,
                    projectedTotal,
                    referencesParsedCycle ? parsedInvoiceTotal : null,
                    difference,
                    currentInstallments.Count,
                    entry.Value.importedNewCount,
                    entry.Value.duplicateCount,
                    referencesParsedCycle && difference.HasValue && Math.Abs(difference.Value) < 0.01m);
            })
            .ToList();

        return new InvoiceReconciliationResponse(
            card.Id,
            card.Nickname,
            request.InvoiceTotal,
            request.DefaultDueDate,
            request.StatementCloseDate,
            items.Count,
            items.Count(x => !x.IsDuplicate),
            items.Count(x => x.IsDuplicate),
            items,
            cycles);
    }

    private async Task<HashSet<string>> LoadExistingKeysAsync(Guid userId, Guid? cardId, CancellationToken cancellationToken)
    {
        var plans = await planRepository.ListByUserAsync(userId, MoneyType.Expense, cancellationToken);
        var installments = await installmentRepository.ListByUserAsync(
            userId,
            null,
            null,
            null,
            MoneyType.Expense,
            cancellationToken);

        var planLookup = plans.ToDictionary(p => p.Id);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installment in installments)
        {
            if (!planLookup.TryGetValue(installment.PlanId, out var plan))
                continue;

            var planCardId = plan.CardId;
            if (cardId.HasValue && planCardId != cardId)
                continue;

            var key = importIdentityEngine.BuildInvoiceImportKey(plan.Title, installment.Amount, installment.DueDate, planCardId);
            keys.Add(key);
        }

        return keys;
    }

    private static string NormalizeTitle(InvoiceImportItemRequest item)
    {
        var baseTitle = !string.IsNullOrWhiteSpace(item.BaseDescription)
            ? item.BaseDescription
            : item.Description;
        baseTitle = Regex.Replace(baseTitle ?? string.Empty, "\\s+", " ").Trim();
        return baseTitle;
    }

    private static string BuildRequestIdempotencyKey(InvoiceImportRequest request)
    {
        var normalized = string.Join('|', request.Items
            .Select(i => $"{(i.BaseDescription ?? i.Description).Trim().ToUpperInvariant()}:{i.Amount}:{i.Date}")
            .OrderBy(x => x, StringComparer.Ordinal));
        var raw = $"{request.CardId}|{request.CategoryId}|{request.DefaultDueDate}|{normalized}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static DateOnly BuildDate(int year, int month, int day)
    {
        var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, safeDay);
    }
}
