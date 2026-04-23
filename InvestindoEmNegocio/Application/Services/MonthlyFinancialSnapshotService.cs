using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class MonthlyFinancialSnapshotService(
    IMonthlyFinancialSnapshotRepository snapshotRepository,
    IAccountAnalyticsService accountAnalyticsService) : IMonthlyFinancialSnapshotService
{
    public async Task<IReadOnlyList<MonthlyFinancialSnapshotResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await snapshotRepository.ListByUserAsync(userId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<MonthlyFinancialSnapshotResponse> GenerateAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new ArgumentException("Competência inválida para snapshot.");

        var existing = await snapshotRepository.GetByMonthAsync(userId, year, month, cancellationToken);
        if (existing is not null)
            return Map(existing);

        var referenceDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var realBalance = await accountAnalyticsService.GetRealAvailableBalanceAsync(userId, "month", referenceDate, cancellationToken);
        var projection = await accountAnalyticsService.GetProjectionAsync(userId, "month", referenceDate, cancellationToken);
        var debts = await accountAnalyticsService.GetDebtSummaryAsync(userId, referenceDate, cancellationToken);
        var netWorth = await accountAnalyticsService.GetNetWorthSummaryAsync(userId, referenceDate, cancellationToken);
        var risk = await accountAnalyticsService.GetRiskAssessmentAsync(userId, "month", referenceDate, cancellationToken);
        var insights = await accountAnalyticsService.GetInsightsAsync(userId, "month", referenceDate, cancellationToken);
        var recommendations = await accountAnalyticsService.GetRecommendationsAsync(userId, "month", referenceDate, cancellationToken);

        var snapshot = new MonthlyFinancialSnapshot(
            userId,
            year,
            month,
            realBalance.RealAvailableBalance,
            projection.ProjectedClosingBalance,
            realBalance.PendingExpensesAmount,
            realBalance.PendingIncomesAmount,
            debts.TotalDebt,
            netWorth.NetWorth,
            risk.Score,
            risk.Classification,
            insights.PrimaryInsight?.Title ?? "Sem insight prioritário",
            JsonSerializer.Serialize(recommendations.Items.Select(x => x.Title).ToList()));

        await snapshotRepository.AddAsync(snapshot, cancellationToken);
        await snapshotRepository.SaveChangesAsync(cancellationToken);
        return Map(snapshot);
    }

    private static MonthlyFinancialSnapshotResponse Map(MonthlyFinancialSnapshot item)
    {
        IReadOnlyList<string> recommendations;
        try
        {
            recommendations = JsonSerializer.Deserialize<List<string>>(item.RecommendationsJson) ?? [];
        }
        catch
        {
            recommendations = [];
        }

        return new MonthlyFinancialSnapshotResponse(
            item.Id,
            item.Year,
            item.Month,
            item.SnapshotLabel,
            item.RealAvailableBalance,
            item.ProjectedBalance,
            item.PendingExpenses,
            item.PendingIncomes,
            item.TotalDebt,
            item.NetWorth,
            item.RiskScore,
            item.RiskClassification,
            item.PrimaryInsight,
            recommendations,
            item.CreatedAt);
    }
}
