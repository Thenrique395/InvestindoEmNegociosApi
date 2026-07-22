using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class ReportService(
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository moneyPlanRepository,
    ICategoryRepository categoryRepository) : IReportService
{
    public async Task<MonthlySummaryReportResponse> GetMonthlySummaryAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var expenses = await installmentRepository.ListByUserAsync(
            userId, InstallmentStatus.Paid, periodStart, periodEnd, MoneyType.Expense, cancellationToken);
        var incomes = await installmentRepository.ListByUserAsync(
            userId, InstallmentStatus.Paid, periodStart, periodEnd, MoneyType.Income, cancellationToken);

        var plans = await moneyPlanRepository.ListByUserAsync(userId, null, cancellationToken);
        var planMap = plans.ToDictionary(p => p.Id);

        var categories = await categoryRepository.ListForUserAsync(userId, null, includeInactive: true, cancellationToken);
        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

        var totalIncome = incomes.Sum(x => x.Amount);
        var totalExpenses = expenses.Sum(x => x.Amount);
        var netBalance = totalIncome - totalExpenses;
        var savingsRate = totalIncome > 0 ? Math.Round(netBalance / totalIncome * 100m, 1) : 0m;

        var expensesByCategory = expenses
            .GroupBy(inst =>
            {
                if (!planMap.TryGetValue(inst.PlanId, out var plan)) return "Sem categoria";
                if (!plan.CategoryId.HasValue) return "Sem categoria";
                return categoryMap.TryGetValue(plan.CategoryId.Value, out var name) ? name : "Sem categoria";
            })
            .Select(g => new
            {
                CategoryName = g.Key,
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var categoryResponses = expensesByCategory
            .Select(x => new CategoryExpenseResponse(
                x.CategoryName,
                x.Amount,
                totalExpenses > 0 ? Math.Round(x.Amount / totalExpenses * 100m, 1) : 0m))
            .ToList();

        var topExpenses = categoryResponses.Take(5).ToList();

        return new MonthlySummaryReportResponse(
            year, month,
            totalIncome, totalExpenses, netBalance, savingsRate,
            categoryResponses,
            topExpenses);
    }
}
