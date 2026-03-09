using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class RecurrenceDetectorService(IInvestDbContext dbContext) : IRecurrenceDetectorService
{
    public async Task<RecurrenceSuggestionDto?> SuggestAsync(
        Guid userId,
        MoneyType type,
        string description,
        decimal amount,
        DateTime? occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeForPattern(description);
        if (string.IsNullOrWhiteSpace(normalized) || amount <= 0)
            return null;

        var recurringPlanSuggestion = await SuggestFromRecurringPlansAsync(userId, type, normalized, amount, cancellationToken);
        if (recurringPlanSuggestion is not null)
            return recurringPlanSuggestion;

        var transactionSuggestion = await SuggestFromTransactionsAsync(userId, type, normalized, amount, occurredAtUtc, cancellationToken);
        if (transactionSuggestion is not null)
            return transactionSuggestion;

        return await SuggestFromPlanHistoryAsync(userId, type, normalized, amount, cancellationToken);
    }

    private async Task<RecurrenceSuggestionDto?> SuggestFromRecurringPlansAsync(
        Guid userId,
        MoneyType type,
        string normalizedDescription,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var recurringPlans = await dbContext.MoneyPlans
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == type && x.Schedule == ScheduleType.Recurring)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var match = recurringPlans.FirstOrDefault(x =>
            MatchesPattern(normalizedDescription, NormalizeForPattern(x.Title))
            && IsAmountSimilar(amount, x.Amount, 0.20m));

        if (match is null)
            return null;

        return BuildSuggestion(match.Frequency?.ToString() ?? "Monthly", 98, "existing_recurring_plan", match.Title);
    }

    private async Task<RecurrenceSuggestionDto?> SuggestFromTransactionsAsync(
        Guid userId,
        MoneyType type,
        string normalizedDescription,
        decimal amount,
        DateTime? occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var kind = type == MoneyType.Expense ? AccountTransactionKind.Debit : AccountTransactionKind.Credit;
        var fromDate = (occurredAtUtc ?? DateTime.UtcNow).AddMonths(-12);

        var transactions = await dbContext.AccountTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Kind == kind && x.OccurredAt >= fromDate)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        var matched = transactions
            .Where(x => MatchesPattern(normalizedDescription, NormalizeForPattern(x.Description)) && IsAmountSimilar(amount, x.Amount, 0.15m))
            .ToList();

        var distinctMonths = matched
            .Select(x => new { x.OccurredAt.Year, x.OccurredAt.Month })
            .Distinct()
            .Count();

        if (matched.Count >= 3 && distinctMonths >= 3)
            return BuildSuggestion("Monthly", 93, "monthly_transaction_pattern", matched.First().Description);
        if (matched.Count >= 2 && distinctMonths >= 2)
            return BuildSuggestion("Monthly", 86, "possible_monthly_pattern", matched.First().Description);

        return null;
    }

    private async Task<RecurrenceSuggestionDto?> SuggestFromPlanHistoryAsync(
        Guid userId,
        MoneyType type,
        string normalizedDescription,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var plans = await dbContext.MoneyPlans
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Type == type)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        var matched = plans
            .Where(x => MatchesPattern(normalizedDescription, NormalizeForPattern(x.Title)) && IsAmountSimilar(amount, x.Amount, 0.20m))
            .ToList();

        var distinctMonths = matched
            .Select(x => new { x.StartDate.Year, x.StartDate.Month })
            .Distinct()
            .Count();

        return distinctMonths >= 2
            ? BuildSuggestion("Monthly", 88, "repeating_plan_history", matched.First().Title)
            : null;
    }

    private static bool MatchesPattern(string incoming, string candidate)
    {
        if (string.IsNullOrWhiteSpace(incoming) || string.IsNullOrWhiteSpace(candidate))
            return false;

        return incoming == candidate
               || incoming.Contains(candidate)
               || candidate.Contains(incoming);
    }

    private static bool IsAmountSimilar(decimal left, decimal right, decimal toleranceRate)
    {
        var baseValue = Math.Max(Math.Abs(left), Math.Abs(right));
        var tolerance = Math.Max(5m, baseValue * toleranceRate);
        return Math.Abs(left - right) <= tolerance;
    }

    private static string NormalizeForPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9\\s]", " ");
        normalized = Regex.Replace(normalized, "\\b\\d+\\b", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized.Trim();
    }

    private static RecurrenceSuggestionDto BuildSuggestion(string frequency, int score, string reasonCode, string evidenceLabel)
    {
        var bounded = Math.Clamp(score, 0, 100);
        var band = bounded >= 95
            ? "high"
            : bounded >= 85
                ? "medium"
                : "low";
        return new RecurrenceSuggestionDto(true, frequency, bounded, band, reasonCode, evidenceLabel);
    }
}
