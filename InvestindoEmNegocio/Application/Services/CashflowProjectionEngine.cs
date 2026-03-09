using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class CashflowProjectionEngine(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    IMoneyInstallmentRepository moneyInstallmentRepository) : ICashflowProjectionEngine
{
    public async Task<CashflowProjectionResponse> ProjectAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizePeriod(period);
        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, periodEnd) = ResolvePeriodRange(anchorDate, normalizedPeriod);

        decimal openingBalance = 0m;
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        foreach (var account in accounts.Where(a => a.IsActive))
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            openingBalance += account.InitialBalance + net;
        }

        var expenses = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            null,
            null,
            periodEnd,
            MoneyType.Expense,
            cancellationToken);
        var incomes = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            InstallmentStatus.Open,
            null,
            periodEnd,
            MoneyType.Income,
            cancellationToken);

        var expensesByDay = expenses
            .Where(IsOpenExpense)
            .Select(item => new
            {
                EffectiveDate = item.DueDate < anchorDate ? anchorDate : item.DueDate,
                item.Amount
            })
            .GroupBy(x => x.EffectiveDate)
            .ToDictionary(g => g.Key, g => new { Amount = g.Sum(x => x.Amount), Count = g.Count() });

        var incomesByDay = incomes
            .Select(item => new
            {
                EffectiveDate = item.DueDate < anchorDate ? anchorDate : item.DueDate,
                item.Amount
            })
            .GroupBy(x => x.EffectiveDate)
            .ToDictionary(g => g.Key, g => new { Amount = g.Sum(x => x.Amount), Count = g.Count() });

        var points = new List<CashflowProjectionPointResponse>();
        var runningBalance = openingBalance;
        var lowestBalance = openingBalance;
        DateOnly? lowestBalanceDate = null;
        DateOnly? riskDate = null;

        for (var cursor = anchorDate; cursor <= periodEnd; cursor = cursor.AddDays(1))
        {
            var opening = runningBalance;
            var incomesForDay = incomesByDay.TryGetValue(cursor, out var incomeDay)
                ? incomeDay
                : new { Amount = 0m, Count = 0 };
            var expensesForDay = expensesByDay.TryGetValue(cursor, out var expenseDay)
                ? expenseDay
                : new { Amount = 0m, Count = 0 };

            runningBalance = opening + incomesForDay.Amount - expensesForDay.Amount;

            if (runningBalance < lowestBalance)
            {
                lowestBalance = runningBalance;
                lowestBalanceDate = cursor;
            }

            if (!riskDate.HasValue && runningBalance < 0m)
                riskDate = cursor;

            points.Add(new CashflowProjectionPointResponse(
                cursor,
                opening,
                incomesForDay.Amount,
                incomesForDay.Count,
                expensesForDay.Amount,
                expensesForDay.Count,
                runningBalance));
        }

        return new CashflowProjectionResponse(
            normalizedPeriod,
            anchorDate,
            anchorDate,
            periodEnd,
            openingBalance,
            runningBalance,
            lowestBalance,
            lowestBalanceDate,
            riskDate,
            points);
    }

    private static bool IsOpenExpense(Domain.Entities.MoneyInstallment item)
    {
        return item.Status is InstallmentStatus.Open or InstallmentStatus.PartiallyPaid;
    }

    private static string NormalizePeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period)) return "month";

        return period.Trim().ToLowerInvariant() switch
        {
            "month" => "month",
            "quarter" => "quarter",
            "year" => "year",
            _ => throw new ArgumentException("Período inválido. Use month, quarter ou year.")
        };
    }

    private static (DateOnly Start, DateOnly End) ResolvePeriodRange(DateOnly anchorDate, string period)
    {
        return period switch
        {
            "year" => (new DateOnly(anchorDate.Year, 1, 1), new DateOnly(anchorDate.Year, 12, 31)),
            "quarter" => ResolveQuarterRange(anchorDate),
            _ => (new DateOnly(anchorDate.Year, anchorDate.Month, 1), new DateOnly(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month)))
        };
    }

    private static (DateOnly Start, DateOnly End) ResolveQuarterRange(DateOnly anchorDate)
    {
        var quarterIndex = (anchorDate.Month - 1) / 3;
        var startMonth = quarterIndex * 3 + 1;
        var endMonth = startMonth + 2;
        return (
            new DateOnly(anchorDate.Year, startMonth, 1),
            new DateOnly(anchorDate.Year, endMonth, DateTime.DaysInMonth(anchorDate.Year, endMonth)));
    }
}
