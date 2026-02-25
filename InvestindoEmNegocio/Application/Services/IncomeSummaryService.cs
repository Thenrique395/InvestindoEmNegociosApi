using System.Globalization;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

public sealed class IncomeSummaryService(
    IPlansService plansService,
    IInstallmentsService installmentsService) : IIncomeSummaryService
{
    public async Task<IncomeSummaryResponse> GetSummaryAsync(Guid userId, string? month, CancellationToken cancellationToken = default)
    {
        var targetMonth = ParseMonthOrNow(month);
        var from = new DateOnly(targetMonth.Year, targetMonth.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var historyStart = targetMonth.AddMonths(-11);
        var historyFrom = new DateOnly(historyStart.Year, historyStart.Month, 1);
        var historyTo = to;

        var plans = await plansService.ListAsync(userId, MoneyType.Income, cancellationToken);
        var installments = await installmentsService.ListAsync(userId, null, historyFrom, historyTo, MoneyType.Income, cancellationToken);

        var planMap = plans.ToDictionary(p => p.Id, p => p);
        var items = installments
            .Where(i => i.DueDate >= from && i.DueDate <= to)
            .Select(i =>
            {
                planMap.TryGetValue(i.PlanId, out var plan);
                var schedule = plan?.Schedule ?? ScheduleType.OneTime;
                var startDate = plan?.StartDate ?? i.DueDate;

                return new IncomeItemResponse(
                    i.Id,
                    i.PlanId,
                    plan?.Title ?? "Receita",
                    i.Amount,
                    i.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    schedule,
                    startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    schedule == ScheduleType.Recurring,
                    schedule == ScheduleType.Recurring
                        ? startDate.ToString("MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
                        : null);
            }).ToList();

        var total = items.Sum(i => i.Amount);
        var totalRecurring = items.Where(i => i.IsRecurring).Sum(i => i.Amount);
        var totalOneTime = total - totalRecurring;

        var monthTotals = new Dictionary<string, (decimal total, decimal recurring)>();
        foreach (var installment in installments)
        {
            planMap.TryGetValue(installment.PlanId, out var plan);
            var schedule = plan?.Schedule ?? ScheduleType.OneTime;
            var isRecurring = schedule == ScheduleType.Recurring;
            var key = installment.DueDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            if (!monthTotals.TryGetValue(key, out var acc))
            {
                acc = (0m, 0m);
            }

            var newTotal = acc.total + installment.Amount;
            var newRecurring = acc.recurring + (isRecurring ? installment.Amount : 0m);
            monthTotals[key] = (newTotal, newRecurring);
        }

        var history = new List<IncomeMonthSummary>();
        for (var i = 0; i < 12; i++)
        {
            var current = historyStart.AddMonths(i);
            var key = $"{current:yyyy-MM}";
            monthTotals.TryGetValue(key, out var acc);
            var recurring = acc.recurring;
            var monthTotal = acc.total;
            history.Add(new IncomeMonthSummary(
                key,
                monthTotal,
                recurring,
                monthTotal - recurring));
        }

        var previousKey = $"{targetMonth.AddMonths(-1):yyyy-MM}";
        var previous = history.FirstOrDefault(h => h.Month == previousKey);

        return new IncomeSummaryResponse(
            $"{targetMonth:yyyy-MM}",
            total,
            totalRecurring,
            totalOneTime,
            items,
            previous,
            history);
    }

    private static DateOnly ParseMonthOrNow(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
