using System.Text;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using UglyToad.PdfPig;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvoiceImportService(
    InvoiceParserFactory parserFactory,
    IPlansService plansService,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository planRepository,
    ICardRepository cardRepository) : IInvoiceImportService
{
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
        return Task.FromResult(parserFactory.Parse(rawText, normalized));
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
                throw new ArgumentException("Cartão selecionado não encontrado.");
        }

        var defaultDate = TryParseDate(request.DefaultDueDate, DateOnly.FromDateTime(DateTime.UtcNow));
        var created = 0;
        var skipped = 0;
        var failed = 0;

        var dedupeKeys = request.SkipDuplicates
            ? await LoadExistingKeysAsync(userId, request.CardId, cancellationToken)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = NormalizeTitle(item);
            if (string.IsNullOrWhiteSpace(title))
            {
                skipped++;
                continue;
            }

            if (!TryParseMoney(item.Amount, out var amount) || amount <= 0m)
            {
                skipped++;
                continue;
            }

            var purchaseDate = TryParseDate(item.Date, defaultDate);
            var dueDate = selectedCard is not null
                ? CardStatementCycleCalculator.Calculate(
                    purchaseDate,
                    selectedCard.StatementCloseDay,
                    selectedCard.DueDay).StatementDueDate
                : purchaseDate;

            var dedupeKey = BuildImportKey(title, amount, dueDate, request.CardId);
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
                CategoryId: request.CategoryId,
                CardId: request.CardId);

            try
            {
                await plansService.CreateAsync(userId, createRequest, cancellationToken);
                dedupeKeys.Add(dedupeKey);
                created++;
            }
            catch
            {
                failed++;
            }
        }

        return new InvoiceImportResultResponse(created, skipped, failed);
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

            var key = BuildImportKey(plan.Title, installment.Amount, installment.DueDate, planCardId);
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

    private static string BuildImportKey(string title, decimal amount, DateOnly dueDate, Guid? cardId)
    {
        return $"{title.ToUpperInvariant()}|{amount:F2}|{dueDate:yyyyMMdd}|{cardId?.ToString() ?? "NO_CARD"}";
    }

    private static bool TryParseMoney(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var cleaned = value
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace(",", ".");

        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static DateOnly TryParseDate(string? value, DateOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        if (DateOnly.TryParseExact(trimmed, "dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), System.Globalization.DateTimeStyles.None, out var full))
            return full;

        if (DateOnly.TryParseExact(trimmed, "dd/MM", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), System.Globalization.DateTimeStyles.None, out var monthDay))
            return new DateOnly(fallback.Year, monthDay.Month, monthDay.Day);

        return fallback;
    }
}
