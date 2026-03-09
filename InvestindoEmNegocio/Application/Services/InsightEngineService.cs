using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InsightEngineService(
    IRiskBotService riskBotService,
    ICashflowProjectionEngine cashflowProjectionEngine,
    IMoneyInstallmentRepository moneyInstallmentRepository,
    IUserProfileRepository userProfileRepository) : IInsightEngineService
{
    public async Task<InsightEngineResponse> BuildAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var risk = await riskBotService.AssessAsync(userId, period, referenceDate, cancellationToken);
        var projection = await cashflowProjectionEngine.ProjectAsync(userId, period, referenceDate, cancellationToken);
        var profile = await userProfileRepository.GetByUserIdAsync(userId, cancellationToken);

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
        var overdueExpensesCount = openExpenses.Count(i => i.DueDate < projection.ReferenceDate);
        var overdueIncomesCount = incomes.Count(i => i.DueDate < projection.ReferenceDate);
        var dueSoonExpensesAmount = openExpenses
            .Where(i => i.DueDate >= projection.ReferenceDate && i.DueDate <= projection.ReferenceDate.AddDays(5))
            .Sum(i => i.Amount);
        var pendingIncomeAmount = incomes.Sum(i => i.Amount);

        var insights = new List<InsightEngineItemResponse>();

        if (overdueExpensesCount > 0 || overdueIncomesCount > 0 || risk.ProjectedBalance < 0m)
        {
            var scenario = overdueExpensesCount > 0
                ? "reactive-overdue-expenses"
                : overdueIncomesCount > 0
                    ? "reactive-overdue-incomes"
                    : "reactive-projected-deficit";
            var title = overdueExpensesCount > 0
                ? "Ação imediata no caixa"
                : overdueIncomesCount > 0
                    ? "Receitas em atraso impactam o caixa"
                    : "Risco de déficit no fechamento";
            var message = overdueExpensesCount > 0
                ? $"Você tem {overdueExpensesCount} despesa(s) vencida(s) exigindo ação imediata."
                : overdueIncomesCount > 0
                    ? $"Há {overdueIncomesCount} receita(s) em atraso comprometendo a leitura do caixa."
                    : "O saldo projetado do período está negativo e precisa de ajuste imediato.";

            insights.Add(BuildItem(
                family: "reactive",
                scenario: scenario,
                priority: "critical",
                title: title,
                message: message,
                action: overdueExpensesCount > 0 ? "Quitar despesas vencidas primeiro." : "Ajustar caixa ainda hoje.",
                risk: risk,
                highlights: [
                    $"Saldo projetado: {risk.ProjectedBalance:N2}",
                    $"Cobertura atual: {risk.CurrentCoverage:N0}%",
                    $"Score: {risk.Score}/100"
                ],
                tips: [
                    "Ataque primeiro os vencimentos mais críticos",
                    "Pause gastos variáveis até estabilizar o caixa",
                    "Confirme receitas já recebidas para limpar o cenário"
                ]));
        }

        if (dueSoonExpensesAmount > 0m || pendingIncomeAmount > 0m)
        {
            insights.Add(BuildItem(
                family: "preventive",
                scenario: "preventive-upcoming-window",
                priority: "warning",
                title: "Janela preventiva dos próximos dias",
                message: $"Há pressão de caixa nos próximos dias com {dueSoonExpensesAmount:N2} em despesas próximas e {pendingIncomeAmount:N2} em receitas pendentes.",
                action: "Acompanhar vencimentos e confirmar entradas antes do fechamento.",
                risk: risk,
                highlights: [
                    $"Próximos 5 dias: {dueSoonExpensesAmount:N2}",
                    $"Receitas pendentes: {pendingIncomeAmount:N2}",
                    $"Cobertura projetada: {risk.ProjectedCoverage:N0}%"
                ],
                tips: [
                    "Reserve caixa para os próximos 5 dias",
                    "Antecipe a confirmação das entradas previsíveis",
                    "Evite novas compras parceladas até virar a janela"
                ]));
        }

        if (risk.CurrentCoverage < 100m || risk.Score < 70)
        {
            insights.Add(BuildItem(
                family: "structural",
                scenario: "structural-coverage-pressure",
                priority: "warning",
                title: "Estrutura de gastos pede ajuste",
                message: "A cobertura do período e o score de risco indicam necessidade de rever compromissos recorrentes e despesas fixas.",
                action: "Revisar categorias de maior peso e cortar pressão estrutural.",
                risk: risk,
                highlights: [
                    $"Cobertura atual: {risk.CurrentCoverage:N0}%",
                    $"Cobertura projetada: {risk.ProjectedCoverage:N0}%",
                    $"Score: {risk.Score}/100"
                ],
                tips: [
                    "Revise despesas recorrentes com maior impacto",
                    "Renegocie contas fixas pressionadas",
                    "Defina teto semanal de gastos variáveis"
                ]));
        }

        if (risk.ProjectedBalance > 0m && risk.Score >= 70)
        {
            var patrimonyGoal = string.IsNullOrWhiteSpace(profile?.FinancialGoal)
                ? "fortalecer metas e patrimônio"
                : $"avançar em {profile!.FinancialGoal}";
            insights.Add(BuildItem(
                family: "patrimonial",
                scenario: "patrimonial-positive-surplus",
                priority: "ok",
                title: "Folga para construir patrimônio",
                message: $"O período projeta superávit e abre espaço para {patrimonyGoal}.",
                action: "Direcionar a folga para metas, reserva ou investimentos.",
                risk: risk,
                highlights: [
                    $"Fechamento projetado: {risk.ProjectedBalance:N2}",
                    $"Score: {risk.Score}/100",
                    $"Objetivo: {profile?.FinancialGoal ?? "Construção patrimonial"}"
                ],
                tips: [
                    "Converta a folga em aporte recorrente",
                    "Priorize reserva e metas de curto prazo",
                    "Evite transformar superávit em gasto pontual"
                ],
                recommendations: [
                    new RiskBotRecommendationResponse(
                        "wealth-surplus",
                        "info",
                        "Direcione a folga do período para metas ou investimentos.",
                        "Abrir metas",
                        "/metas",
                        new Dictionary<string, string>(),
                        risk.ProjectedBalance,
                        null)
                ]));
        }

        var ordered = insights
            .OrderByDescending(x => PriorityWeight(x.Priority))
            .ThenByDescending(x => FamilyWeight(x.Family))
            .ToList();

        return new InsightEngineResponse(
            risk.Period,
            risk.ReferenceDate,
            ordered.FirstOrDefault(),
            ordered);
    }

    private static InsightEngineItemResponse BuildItem(
        string family,
        string scenario,
        string priority,
        string title,
        string message,
        string action,
        RiskBotAssessmentResponse risk,
        IReadOnlyList<string> highlights,
        IReadOnlyList<string> tips,
        IReadOnlyList<RiskBotRecommendationResponse>? recommendations = null)
    {
        return new InsightEngineItemResponse(
            family,
            scenario,
            priority,
            title,
            message,
            action,
            risk.Score,
            risk.RiskDate,
            risk.CurrentCoverage,
            risk.ProjectedCoverage,
            risk.ProjectedBalance,
            highlights,
            tips,
            risk.ReasonCodes,
            risk.ScoreBreakdown,
            recommendations ?? risk.Recommendations);
    }

    private static int PriorityWeight(string priority)
    {
        return priority switch
        {
            "critical" => 3,
            "warning" => 2,
            _ => 1
        };
    }

    private static int FamilyWeight(string family)
    {
        return family switch
        {
            "reactive" => 4,
            "preventive" => 3,
            "structural" => 2,
            "patrimonial" => 1,
            _ => 0
        };
    }
}
