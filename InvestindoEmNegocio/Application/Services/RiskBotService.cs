using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class RiskBotService(
    IMoneyInstallmentRepository moneyInstallmentRepository,
    ICashflowProjectionEngine cashflowProjectionEngine) : IRiskBotService
{
    public async Task<RiskBotAssessmentResponse> AssessAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var projection = await cashflowProjectionEngine.ProjectAsync(userId, period, referenceDate, cancellationToken);
        var expenses = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            null,
            projection.ProjectionStart,
            projection.ProjectionEnd,
            MoneyType.Expense,
            cancellationToken);
        var incomes = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            InstallmentStatus.Open,
            projection.ProjectionStart,
            projection.ProjectionEnd,
            MoneyType.Income,
            cancellationToken);

        var openExpenses = expenses.Where(i => i.Status is InstallmentStatus.Open or InstallmentStatus.PartiallyPaid).ToList();
        var overdueExpenses = openExpenses.Where(i => i.DueDate < projection.ReferenceDate).ToList();
        var overdueIncomes = incomes.Where(i => i.DueDate < projection.ReferenceDate).ToList();
        var dueSoonExpenses = openExpenses
            .Where(i => i.DueDate >= projection.ReferenceDate && i.DueDate <= projection.ReferenceDate.AddDays(5))
            .ToList();

        var pendingIncome = incomes.Sum(i => i.Amount);
        var pendingExpense = openExpenses.Sum(i => i.Amount);
        var currentCoverage = pendingExpense > 0m ? (projection.OpeningBalance / pendingExpense) * 100m : 100m;
        var projectedCoverage = pendingExpense > 0m ? (projection.ProjectedClosingBalance + pendingExpense) / pendingExpense * 100m : 100m;
        var score = CalculateHealthScore(
            overdueExpenses.Count,
            overdueIncomes.Count,
            dueSoonExpenses.Sum(i => i.Amount),
            projection.ProjectedClosingBalance,
            currentCoverage);
        var priority = DeterminePriority(projection.ProjectedClosingBalance, overdueExpenses.Count, overdueIncomes.Count, pendingIncome, score);
        var classification = DetermineClassification(score, priority);
        var reasonCodes = BuildReasonCodes(
            projection.ProjectedClosingBalance,
            overdueExpenses.Count,
            overdueIncomes.Count,
            pendingIncome,
            dueSoonExpenses.Sum(i => i.Amount),
            score);
        var breakdown = BuildScoreBreakdown(
            overdueExpenses.Count,
            overdueIncomes.Count,
            dueSoonExpenses.Sum(i => i.Amount),
            projection.ProjectedClosingBalance,
            currentCoverage);
        var recommendations = BuildRecommendations(overdueExpenses, overdueIncomes, dueSoonExpenses, incomes);

        return new RiskBotAssessmentResponse(
            projection.Period,
            projection.ReferenceDate,
            score,
            classification,
            priority,
            projection.RiskDate,
            decimal.Round(currentCoverage, 2),
            decimal.Round(projectedCoverage, 2),
            projection.ProjectedClosingBalance,
            reasonCodes,
            breakdown,
            recommendations);
    }

    private static int CalculateHealthScore(
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal dueSoonExpenseAmount,
        decimal projectedBalance,
        decimal currentCoverage)
    {
        var score = 100;
        if (overdueExpensesCount > 0) score -= Math.Min(35, overdueExpensesCount * 12);
        if (overdueIncomesCount > 0) score -= Math.Min(20, overdueIncomesCount * 8);
        if (currentCoverage < 60m) score -= 15;
        if (dueSoonExpenseAmount > 0m && currentCoverage < 100m) score -= 10;
        if (projectedBalance < 0m) score -= 20;
        return Math.Clamp(score, 0, 100);
    }

    private static string DeterminePriority(decimal projectedBalance, int overdueExpensesCount, int overdueIncomesCount, decimal pendingIncome, int score)
    {
        if (projectedBalance < 0m || overdueExpensesCount > 0 || score < 45) return "critical";
        if (overdueIncomesCount > 0 || pendingIncome > 0m || score < 70) return "warning";
        return "ok";
    }

    private static string DetermineClassification(int score, string priority)
    {
        if (string.Equals(priority, "critical", StringComparison.OrdinalIgnoreCase)) return "critical";
        if (score < 45) return "critical";
        if (score < 70) return "warning";
        return "healthy";
    }

    private static IReadOnlyList<string> BuildReasonCodes(
        decimal projectedBalance,
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal pendingIncome,
        decimal dueSoonExpenseAmount,
        int score)
    {
        var reasons = new List<string>();
        if (projectedBalance < 0m) reasons.Add("projected_deficit");
        if (overdueExpensesCount > 0) reasons.Add("overdue_expenses");
        if (overdueIncomesCount > 0) reasons.Add("overdue_incomes");
        if (pendingIncome > 0m) reasons.Add("pending_income");
        if (dueSoonExpenseAmount > 0m) reasons.Add("due_soon_expenses");
        if (score < 45) reasons.Add("health_score_critical");
        else if (score < 70) reasons.Add("health_score_warning");
        return reasons;
    }

    private static IReadOnlyList<string> BuildScoreBreakdown(
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal dueSoonExpenseAmount,
        decimal projectedBalance,
        decimal currentCoverage)
    {
        var breakdown = new List<string> { "Base: 100" };
        if (overdueExpensesCount > 0) breakdown.Add($"- {Math.Min(35, overdueExpensesCount * 12)} despesas atrasadas");
        if (overdueIncomesCount > 0) breakdown.Add($"- {Math.Min(20, overdueIncomesCount * 8)} receitas atrasadas");
        if (currentCoverage < 60m) breakdown.Add("- 15 cobertura atual abaixo de 60%");
        if (dueSoonExpenseAmount > 0m && currentCoverage < 100m) breakdown.Add("- 10 pressão de vencimentos próximos");
        if (projectedBalance < 0m) breakdown.Add("- 20 saldo projetado negativo");
        return breakdown;
    }

    private static IReadOnlyList<RiskBotRecommendationResponse> BuildRecommendations(
        IReadOnlyList<Domain.Entities.MoneyInstallment> overdueExpenses,
        IReadOnlyList<Domain.Entities.MoneyInstallment> overdueIncomes,
        IReadOnlyList<Domain.Entities.MoneyInstallment> dueSoonExpenses,
        IReadOnlyList<Domain.Entities.MoneyInstallment> openIncomes)
    {
        var items = new List<RiskBotRecommendationResponse>();

        if (overdueExpenses.Count > 0)
        {
            items.Add(new RiskBotRecommendationResponse(
                "overdue-expenses",
                "danger",
                $"Você tem {overdueExpenses.Count} despesa(s) vencida(s).",
                "Quitar despesas",
                "/despesas",
                new Dictionary<string, string> { ["focus"] = "overdue" },
                overdueExpenses.Sum(i => i.Amount),
                overdueExpenses.Min(i => i.DueDate)));
        }

        if (overdueIncomes.Count > 0)
        {
            items.Add(new RiskBotRecommendationResponse(
                "overdue-incomes",
                "warn",
                $"Você tem {overdueIncomes.Count} receita(s) em atraso.",
                "Confirmar recebimentos",
                "/receitas",
                new Dictionary<string, string> { ["focus"] = "pending" },
                overdueIncomes.Sum(i => i.Amount),
                overdueIncomes.Min(i => i.DueDate)));
        }

        if (dueSoonExpenses.Count > 0)
        {
            items.Add(new RiskBotRecommendationResponse(
                "due-soon-expenses",
                "warn",
                $"Há {dueSoonExpenses.Count} despesa(s) vencendo nos próximos 5 dias.",
                "Ver próximas despesas",
                "/despesas",
                new Dictionary<string, string> { ["focus"] = "upcoming" },
                dueSoonExpenses.Sum(i => i.Amount),
                dueSoonExpenses.Min(i => i.DueDate)));
        }

        var nextIncome = openIncomes.Where(i => i.DueDate >= DateOnly.MinValue).OrderBy(i => i.DueDate).FirstOrDefault();
        if (nextIncome is not null)
        {
            items.Add(new RiskBotRecommendationResponse(
                "pending-income",
                "info",
                $"Próxima receita pendente em {nextIncome.DueDate:dd/MM/yyyy}.",
                "Abrir receitas",
                "/receitas",
                new Dictionary<string, string> { ["focus"] = "pending" },
                nextIncome.Amount,
                nextIncome.DueDate));
        }

        return items.Take(4).ToList();
    }
}
