using System.Globalization;
using System.Text.Json;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestindoEmNegocio.Application.Services;

public sealed class NotificationGenerationService(
    IUserNotificationRepository notificationRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository planRepository,
    IUserProfileRepository profileRepository,
    INotificationSettingsRepository settingsRepository,
    ICardRepository cardRepository,
    IGoalRepository goalRepository,
    IGoalContributionRepository goalContributionRepository,
    ILogger<NotificationGenerationService>? logger = null) : INotificationGenerationService
{
    private readonly ILogger<NotificationGenerationService> _logger = logger ?? NullLogger<NotificationGenerationService>.Instance;

    public async Task<int> GenerateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
            return 0;

        if (!profile.NotifyInAppEnabled)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken);
        var candidates = new List<UserNotification>();
        var culture = new CultureInfo("pt-BR");

        if (settings.IncomeUpcomingEnabled || settings.ExpenseUpcomingEnabled || settings.ExpenseOverdueEnabled)
        {
            var installments = await installmentRepository.ListByUserAsync(userId, null, null, null, null, cancellationToken);
            var open = installments.Where(i => i.Status is InstallmentStatus.Open or InstallmentStatus.PartiallyPaid).ToList();

            var planIds = open.Select(i => i.PlanId).Distinct().ToList();
            var plans = await planRepository.ListByUserAsync(userId, null, cancellationToken);
            var planLookup = plans.Where(p => planIds.Contains(p.Id)).ToDictionary(p => p.Id);

            if (settings.IncomeUpcomingEnabled && settings.IncomeDaysBefore > 0)
            {
                var upcomingLimit = today.AddDays(settings.IncomeDaysBefore);
                foreach (var installment in open.Where(i => i.DueDate >= today && i.DueDate <= upcomingLimit))
                {
                    if (!planLookup.TryGetValue(installment.PlanId, out var plan))
                        continue;
                    if (plan.Type != MoneyType.Income)
                        continue;

                    var referenceKey = $"installment:{installment.Id}:income:{installment.DueDate:yyyyMMdd}";
                    var title = $"Receita recebe em {installment.DueDate:dd/MM}";
                    var message = $"{plan.Title} · R$ {installment.Amount.ToString("N2", culture)}";
                    candidates.Add(new UserNotification(userId, NotificationKind.IncomeUpcoming, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
                }
            }

            if (settings.ExpenseUpcomingEnabled && settings.ExpenseDaysBefore > 0)
            {
                var upcomingLimit = today.AddDays(settings.ExpenseDaysBefore);
                foreach (var installment in open.Where(i => i.DueDate >= today && i.DueDate <= upcomingLimit))
                {
                    if (!planLookup.TryGetValue(installment.PlanId, out var plan))
                        continue;
                    if (plan.Type != MoneyType.Expense)
                        continue;

                    var referenceKey = $"installment:{installment.Id}:expense:{installment.DueDate:yyyyMMdd}";
                    var title = $"Despesa vence em {installment.DueDate:dd/MM}";
                    var message = $"{plan.Title} · R$ {installment.Amount.ToString("N2", culture)}";
                    candidates.Add(new UserNotification(userId, NotificationKind.ExpenseUpcoming, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
                }
            }

            if (settings.ExpenseOverdueEnabled)
            {
                foreach (var installment in open.Where(i => i.DueDate < today))
                {
                    if (!planLookup.TryGetValue(installment.PlanId, out var plan))
                        continue;
                    if (plan.Type != MoneyType.Expense)
                        continue;

                    var referenceKey = $"installment:{installment.Id}:expense-overdue:{installment.DueDate:yyyyMMdd}";
                    var title = "Despesa atrasada";
                    var message = $"{plan.Title} · Venceu em {installment.DueDate:dd/MM}";
                    candidates.Add(new UserNotification(userId, NotificationKind.ExpenseOverdue, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
                }
            }
        }

        if (settings.CardCloseSoonEnabled || settings.CardCloseDayEnabled)
        {
            var cards = await cardRepository.ListByUserAsync(userId, cancellationToken);
            foreach (var card in cards)
            {
                var closeDate = ResolveMonthlyDate(today, card.StatementCloseDay);
                var daysUntil = closeDate.DayNumber - today.DayNumber;

                if (settings.CardCloseDayEnabled && closeDate == today)
                {
                    var referenceKey = $"card-close-day:{card.Id}:{closeDate:yyyyMMdd}";
                    var title = $"{card.Nickname} fecha hoje";
                    var message = $"Fatura fecha em {closeDate:dd/MM}.";
                    candidates.Add(new UserNotification(userId, NotificationKind.CardClosingDay, title, message, referenceKey, null, null, null, closeDate));
                }

                if (settings.CardCloseSoonEnabled && settings.CardCloseDaysBefore > 0 && daysUntil > 0 && daysUntil <= settings.CardCloseDaysBefore)
                {
                    var referenceKey = $"card-close-soon:{card.Id}:{closeDate:yyyyMMdd}";
                    var title = $"Fatura fecha em {daysUntil} dias";
                    var message = $"{card.Nickname} · Fecha em {closeDate:dd/MM}";
                    candidates.Add(new UserNotification(userId, NotificationKind.CardClosingSoon, title, message, referenceKey, null, null, null, closeDate));
                }
            }
        }

        if (settings.MonthCloseEnabled && IsLastDayOfMonth(today))
        {
            var referenceKey = $"month-close:{today:yyyyMM}";
            var title = "Fechamento do mês";
            var message = $"Hoje fecha o mês de {today.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture)}.";
            candidates.Add(new UserNotification(userId, NotificationKind.MonthClosing, title, message, referenceKey, null, null, null, today));
        }

        if (settings.MonthSummaryEnabled && today.Day == 1)
        {
            var referenceKey = $"month-summary:{today:yyyyMM}";
            var previousMonth = today.AddDays(-1);
            var title = "Resumo mensal disponível";
            var message = $"Resumo de {previousMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture)} pronto para revisão.";
            candidates.Add(new UserNotification(userId, NotificationKind.MonthSummary, title, message, referenceKey, null, null, null, today));
        }

        if (settings.MonthSummaryEnabled)
        {
            var insight = await BuildCashflowInsightAsync(
                userId,
                today,
                culture,
                profile.FinancialGoal,
                profile.CarryOverDay,
                cancellationToken);
            if (insight is not null)
                candidates.Add(insight);
        }

        if (settings.GoalBelowExpectedEnabled || settings.GoalCompletedEnabled || settings.GoalInactivityEnabled)
        {
            var goals = await goalRepository.ListByUserAsync(userId, null, null, cancellationToken);
            var goalLastContributionDates = settings.GoalInactivityEnabled && settings.GoalInactivityDays > 0
                ? await goalContributionRepository.GetLastContributionDatesByGoalsAsync(userId, goals.Select(g => g.Id), cancellationToken)
                : new Dictionary<Guid, DateOnly>();
            if (goalLastContributionDates is null && settings.GoalInactivityEnabled && settings.GoalInactivityDays > 0)
            {
                goalLastContributionDates = new Dictionary<Guid, DateOnly>();
                foreach (var goal in goals)
                {
                    var contributions = await goalContributionRepository.ListByGoalAsync(goal.Id, userId, cancellationToken);
                    if (contributions.Count > 0)
                        goalLastContributionDates[goal.Id] = contributions.Max(c => c.Date);
                }
            }
            goalLastContributionDates ??= new Dictionary<Guid, DateOnly>();
            foreach (var goal in goals)
            {
                if (settings.GoalCompletedEnabled && goal.Status == GoalStatus.Completed)
                {
                    var referenceKey = $"goal-completed:{goal.Id}";
                    var title = "Meta atingida";
                    var message = $"{goal.Title} · R$ {goal.TargetAmount.ToString("N2", culture)}";
                    candidates.Add(new UserNotification(userId, NotificationKind.GoalCompleted, title, message, referenceKey, null, null, null, null));
                }

                if (settings.GoalBelowExpectedEnabled && goal.ExpectedMonthly > 0 && goal.Status is not GoalStatus.Completed and not GoalStatus.Canceled)
                {
                    var expected = goal.ExpectedMonthly * today.Month;
                    if (expected > 0 && goal.CurrentAmount < expected)
                    {
                        var referenceKey = $"goal-below:{goal.Id}:{today:yyyyMM}";
                        var title = "Meta abaixo do esperado";
                        var message = $"{goal.Title} · R$ {goal.CurrentAmount.ToString("N2", culture)} de R$ {expected.ToString("N2", culture)}";
                        candidates.Add(new UserNotification(userId, NotificationKind.GoalBelowExpected, title, message, referenceKey, null, null, null, null));
                    }
                }

                if (settings.GoalInactivityEnabled && settings.GoalInactivityDays > 0 && goal.Status is not GoalStatus.Completed and not GoalStatus.Canceled)
                {
                    var lastDate = goalLastContributionDates.TryGetValue(goal.Id, out var lastContribution)
                        ? lastContribution
                        : DateOnly.FromDateTime(goal.CreatedAt);
                    var daysSince = today.DayNumber - lastDate.DayNumber;
                    if (daysSince >= settings.GoalInactivityDays)
                    {
                        var referenceKey = $"goal-inactive:{goal.Id}:{today:yyyyMM}";
                        var title = "Meta sem movimentações";
                        var message = $"{goal.Title} · Sem aportes há {daysSince} dias.";
                        candidates.Add(new UserNotification(userId, NotificationKind.GoalInactive, title, message, referenceKey, null, null, null, null));
                    }
                }
            }
        }

        var uniqueCandidates = candidates
            .GroupBy(n => n.ReferenceKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (uniqueCandidates.Count == 0)
            return 0;

        var existingReferenceKeys = await notificationRepository.ListReferenceKeysAsync(
            userId,
            uniqueCandidates.Select(n => n.ReferenceKey),
            cancellationToken);
        if (existingReferenceKeys is null)
        {
            existingReferenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var referenceKey in uniqueCandidates.Select(n => n.ReferenceKey))
            {
                if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                    existingReferenceKeys.Add(referenceKey);
            }
        }

        var toCreate = uniqueCandidates
            .Where(n => !existingReferenceKeys.Contains(n.ReferenceKey))
            .ToList();
        if (toCreate.Count == 0)
        {
            _logger.LogInformation(
                "Notifications generate finished for {UserId}. candidates={Candidates} created=0 durationMs={DurationMs}",
                userId,
                uniqueCandidates.Count,
                (DateTime.UtcNow - startedAt).TotalMilliseconds);
            return 0;
        }

        await notificationRepository.AddRangeAsync(toCreate, cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notifications generate finished for {UserId}. candidates={Candidates} created={Created} durationMs={DurationMs}",
            userId,
            uniqueCandidates.Count,
            toCreate.Count,
            (DateTime.UtcNow - startedAt).TotalMilliseconds);
        return toCreate.Count;
    }

    private static DateOnly ResolveMonthlyDate(DateOnly today, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var adjustedDay = Math.Min(day, daysInMonth);
        var candidate = new DateOnly(today.Year, today.Month, adjustedDay);
        if (candidate < today)
        {
            var next = today.AddMonths(1);
            var nextDays = DateTime.DaysInMonth(next.Year, next.Month);
            var nextDay = Math.Min(day, nextDays);
            return new DateOnly(next.Year, next.Month, nextDay);
        }
        return candidate;
    }

    private static bool IsLastDayOfMonth(DateOnly date)
    {
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        return date.Day == daysInMonth;
    }

    private async Task<UserNotification?> BuildCashflowInsightAsync(
        Guid userId,
        DateOnly today,
        CultureInfo culture,
        string? financialGoal,
        int carryOverDay,
        CancellationToken cancellationToken)
    {
        var installments = await installmentRepository.ListByUserAsync(userId, null, null, null, null, cancellationToken);
        if (installments.Count == 0) return null;

        var planIds = installments.Select(i => i.PlanId).Distinct().ToList();
        var plans = await planRepository.ListByUserAsync(userId, null, cancellationToken);
        var planLookup = plans.Where(p => planIds.Contains(p.Id)).ToDictionary(p => p.Id);

        var (competenceStart, competenceEnd) = CompetenceWindowCalculator.Resolve(today, carryOverDay);

        var monthInstallments = installments
            .Where(i => i.DueDate >= competenceStart && i.DueDate <= competenceEnd)
            .Where(i => planLookup.ContainsKey(i.PlanId))
            .ToList();

        if (monthInstallments.Count == 0) return null;

        static bool IsReceivedStatus(InstallmentStatus status) =>
            status is InstallmentStatus.Paid or InstallmentStatus.PartiallyPaid or InstallmentStatus.Anticipated;
        static bool IsOpenStatus(InstallmentStatus status) =>
            status is InstallmentStatus.Open or InstallmentStatus.PartiallyPaid;

        var incomeReceived = monthInstallments
            .Where(i => planLookup[i.PlanId].Type == MoneyType.Income &&
                        IsReceivedStatus(i.Status))
            .Sum(i => i.Amount);

        var incomePending = monthInstallments
            .Where(i => planLookup[i.PlanId].Type == MoneyType.Income &&
                        i.Status is InstallmentStatus.Open)
            .Sum(i => i.Amount);

        var expenseTotal = monthInstallments
            .Where(i => planLookup[i.PlanId].Type == MoneyType.Expense)
            .Sum(i => i.Amount);

        var openExpenses = monthInstallments
            .Where(i => planLookup[i.PlanId].Type == MoneyType.Expense && IsOpenStatus(i.Status))
            .ToList();
        var openIncomes = monthInstallments
            .Where(i => planLookup[i.PlanId].Type == MoneyType.Income && i.Status is InstallmentStatus.Open)
            .ToList();

        var overdueExpenses = openExpenses.Where(i => i.DueDate < today).ToList();
        var overdueIncomes = openIncomes.Where(i => i.DueDate < today).ToList();
        var dueSoonExpenses = openExpenses.Where(i => i.DueDate >= today && i.DueDate <= today.AddDays(5)).ToList();
        var overdueExpensesAmount = overdueExpenses.Sum(i => i.Amount);
        var hasCoverageForOverdueExpenses = incomeReceived >= overdueExpensesAmount;
        var hasCriticalOverdueExpenses = overdueExpenses.Count > 0 && !hasCoverageForOverdueExpenses;

        if (incomePending <= 0m && expenseTotal <= 0m && overdueExpenses.Count == 0 && overdueIncomes.Count == 0) return null;

        var coverage = expenseTotal > 0m ? (incomeReceived / expenseTotal) * 100m : 100m;
        var projectedCoverage = expenseTotal > 0m ? ((incomeReceived + incomePending) / expenseTotal) * 100m : 100m;
        var projected = incomeReceived + incomePending - expenseTotal;
        var healthScore = CalculateHealthScore(
            incomeReceived,
            incomePending,
            expenseTotal,
            overdueExpenses.Count,
            overdueIncomes.Count,
            dueSoonExpenses.Sum(i => i.Amount),
            projected);
        var riskDay = EstimateRiskDay(today, competenceEnd, incomeReceived, openIncomes, openExpenses);

        var scenario = "stable";
        var title = "Insight do mês";
        var action = "Ação recomendada: revise receitas e despesas.";

        if (overdueExpenses.Count > 0)
        {
            if (hasCriticalOverdueExpenses)
            {
                scenario = "critical-overdue-expenses";
                title = "Ação imediata: despesas atrasadas";
                action = "Ação recomendada: regularize primeiro as despesas vencidas.";
            }
            else
            {
                scenario = "overdue-expenses-covered";
                title = "Despesas vencidas com cobertura disponível";
                action = "Ação recomendada: quite as despesas vencidas e dê baixa no sistema.";
            }
        }
        else if (projected < 0m)
        {
            scenario = "projected-deficit";
            title = "Risco de fechar o mês no negativo";
            action = "Ação recomendada: reduzir despesas ou antecipar receitas.";
        }
        else if (overdueIncomes.Count > 0)
        {
            scenario = "overdue-incomes";
            title = "Receitas vencidas sem confirmação";
            action = "Ação recomendada: confirme recebimentos em atraso.";
        }
        else if (incomeReceived <= 0m && incomePending > 0m && expenseTotal > 0m)
        {
            scenario = "pending-risk";
            title = "Fluxo em risco: receitas pendentes";
            action = "Ação recomendada: confirme recebimentos em Receitas.";
        }
        else if (incomeReceived <= 0m && expenseTotal > 0m)
        {
            scenario = "no-income";
            title = "Sem receita recebida no mês";
            action = "Ação recomendada: registre/receba receita para atualizar o saldo.";
        }
        else if (coverage < 100m && incomePending > 0m)
        {
            scenario = "partial-coverage";
            title = "Cobertura parcial das despesas";
            action = "Ação recomendada: priorize despesas críticas e confirme receitas pendentes.";
        }
        else if (incomePending > 0m)
        {
            scenario = "pending-confirmation";
            title = "Receita pendente de confirmação";
            action = "Ação recomendada: marque os recebimentos pendentes.";
        }
        else
        {
            return null;
        }

        var monthLabel = $"{competenceStart:dd/MM} a {competenceEnd:dd/MM}";
        var objectiveSuffix = string.IsNullOrWhiteSpace(financialGoal) ? string.Empty : $" Objetivo: {financialGoal}.";
        var riskDaySuffix = riskDay.HasValue ? $" Dia de risco: {riskDay:dd/MM}." : string.Empty;
        var tips = BuildCashflowTips(scenario);
        var priority = DetermineInsightPriority(
            projected,
            hasCriticalOverdueExpenses,
            overdueExpenses.Count,
            overdueIncomes.Count,
            incomePending,
            healthScore);
        var recommendations = BuildCashflowRecommendations(
            today,
            overdueExpenses,
            overdueIncomes,
            dueSoonExpenses,
            openIncomes,
            hasCriticalOverdueExpenses);
        var reasonCodes = BuildReasonCodes(
            projected,
            hasCriticalOverdueExpenses,
            overdueExpenses.Count,
            overdueIncomes.Count,
            incomePending,
            dueSoonExpenses.Sum(i => i.Amount),
            healthScore);
        var scoreBreakdown = BuildCashflowScoreBreakdown(
            incomeReceived,
            incomePending,
            expenseTotal,
            overdueExpenses.Count,
            overdueIncomes.Count,
            dueSoonExpenses.Sum(i => i.Amount),
            projected);
        var payload = new
        {
            scenario,
            priority,
            healthScore,
            riskDay = riskDay?.ToString("yyyy-MM-dd"),
            overdueExpenses = overdueExpenses.Count,
            overdueExpensesAmount,
            overdueExpensesCovered = hasCoverageForOverdueExpenses,
            overdueIncomes = overdueIncomes.Count,
            dueSoonExpensesAmount = dueSoonExpenses.Sum(i => i.Amount),
            currentCoverage = decimal.Round(coverage, 2),
            projectedCoverage = decimal.Round(projectedCoverage, 2),
            projectedBalance = projected,
            action,
            reasonCodes,
            recommendations,
            tips,
            scoreBreakdown
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        var tipsSuffix = tips.Count > 0 ? $" Dicas: {string.Join(" | ", tips)}." : string.Empty;
        var message =
            $"{monthLabel}: recebidas {incomeReceived.ToString("N2", culture)}, pendentes {incomePending.ToString("N2", culture)}, despesas {expenseTotal.ToString("N2", culture)}. " +
            $"Cobertura atual {coverage.ToString("N0", culture)}%, cobertura projetada {projectedCoverage.ToString("N0", culture)}%, saldo projetado {projected.ToString("N2", culture)} e saúde financeira {healthScore}/100. " +
            $"Atrasos: {overdueExpenses.Count} despesa(s) e {overdueIncomes.Count} receita(s).{riskDaySuffix} {action}{objectiveSuffix}{tipsSuffix}";

        var referenceKey = $"cashflow-insight:{today:yyyyMMdd}:{scenario}";
        return new UserNotification(
            userId,
            NotificationKind.CashflowInsight,
            title,
            message,
            referenceKey,
            null,
            null,
            null,
            today,
            payloadJson);
    }

    private static string DetermineInsightPriority(
        decimal projectedBalance,
        bool hasCriticalOverdueExpenses,
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal pendingIncome,
        int healthScore)
    {
        if (projectedBalance < 0m || hasCriticalOverdueExpenses || healthScore < 45)
            return "critical";

        if (overdueExpensesCount > 0 || overdueIncomesCount > 0 || pendingIncome > 0m || healthScore < 70)
            return "warning";

        return "ok";
    }

    private static IReadOnlyList<object> BuildCashflowRecommendations(
        DateOnly today,
        IReadOnlyList<MoneyInstallment> overdueExpenses,
        IReadOnlyList<MoneyInstallment> overdueIncomes,
        IReadOnlyList<MoneyInstallment> dueSoonExpenses,
        IReadOnlyList<MoneyInstallment> openIncomes,
        bool hasCriticalOverdueExpenses)
    {
        var recommendations = new List<(int score, object payload)>();

        if (overdueExpenses.Count > 0)
        {
            var firstDue = overdueExpenses.Min(i => i.DueDate).ToString("dd/MM/yyyy");
            var amount = overdueExpenses.Sum(i => i.Amount);
            recommendations.Add((
                hasCriticalOverdueExpenses ? 100 : 80,
                new
                {
                    id = "overdue-expenses",
                    severity = hasCriticalOverdueExpenses ? "danger" : "warn",
                    text = hasCriticalOverdueExpenses
                        ? $"Você tem {overdueExpenses.Count} despesa(s) vencida(s) desde {firstDue} e o caixa não cobre o total atrasado."
                        : $"Você tem {overdueExpenses.Count} despesa(s) vencida(s) desde {firstDue}, com caixa para quitar.",
                    actionLabel = hasCriticalOverdueExpenses ? "Quitar despesas" : "Quitar e dar baixa",
                    route = "/expenses",
                    queryParams = new { focus = "overdue" },
                    amount,
                    dueDate = firstDue
                }));
        }

        if (overdueIncomes.Count > 0)
        {
            var firstDue = overdueIncomes.Min(i => i.DueDate).ToString("dd/MM/yyyy");
            recommendations.Add((
                75,
                new
                {
                    id = "overdue-incomes",
                    severity = "warn",
                    text = $"Você tem {overdueIncomes.Count} receita(s) em atraso (mais antiga em {firstDue}).",
                    actionLabel = "Confirmar recebimentos",
                    route = "/incomes",
                    queryParams = new { focus = "pending" },
                    amount = overdueIncomes.Sum(i => i.Amount),
                    dueDate = firstDue
                }));
        }

        if (dueSoonExpenses.Count > 0)
        {
            var firstDue = dueSoonExpenses.Min(i => i.DueDate).ToString("dd/MM/yyyy");
            recommendations.Add((
                60,
                new
                {
                    id = "due-soon-expenses",
                    severity = "warn",
                    text = $"Há {dueSoonExpenses.Count} despesa(s) vencendo nos próximos 5 dias (primeira em {firstDue}).",
                    actionLabel = "Ver próximas despesas",
                    route = "/expenses",
                    queryParams = new { focus = "upcoming" },
                    amount = dueSoonExpenses.Sum(i => i.Amount),
                    dueDate = firstDue
                }));
        }

        if (openIncomes.Count > 0)
        {
            var nearest = openIncomes
                .Where(i => i.DueDate >= today)
                .OrderBy(i => i.DueDate)
                .FirstOrDefault();
            if (nearest is not null)
            {
                recommendations.Add((
                    50,
                    new
                    {
                        id = "pending-income-nearest",
                        severity = "info",
                        text = $"Próxima receita pendente em {nearest.DueDate:dd/MM/yyyy}.",
                        actionLabel = "Abrir receitas",
                        route = "/incomes",
                        queryParams = new { focus = "pending" },
                        amount = nearest.Amount,
                        dueDate = nearest.DueDate.ToString("dd/MM/yyyy")
                    }));
            }
        }

        return recommendations
            .OrderByDescending(i => i.score)
            .Take(4)
            .Select(i => i.payload)
            .ToList();
    }

    private static IReadOnlyList<string> BuildReasonCodes(
        decimal projectedBalance,
        bool hasCriticalOverdueExpenses,
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal pendingIncome,
        decimal dueSoonExpenseAmount,
        int healthScore)
    {
        var reasons = new List<string>();
        if (projectedBalance < 0m) reasons.Add("projected_deficit");
        if (hasCriticalOverdueExpenses) reasons.Add("overdue_expenses_uncovered");
        if (overdueExpensesCount > 0 && !hasCriticalOverdueExpenses) reasons.Add("overdue_expenses_covered");
        if (overdueIncomesCount > 0) reasons.Add("overdue_incomes");
        if (pendingIncome > 0m) reasons.Add("pending_income");
        if (dueSoonExpenseAmount > 0m) reasons.Add("due_soon_expenses");
        if (healthScore < 45) reasons.Add("health_score_critical");
        else if (healthScore < 70) reasons.Add("health_score_warning");
        return reasons;
    }

    private static IReadOnlyList<string> BuildCashflowTips(string scenario)
    {
        return scenario switch
        {
            "critical-overdue-expenses" => [
                "Quite primeiro as despesas vencidas com juros mais altos",
                "Pause gastos variáveis até regularizar o atraso",
                "Renegocie vencimentos críticos se necessário"
            ],
            "overdue-expenses-covered" => [
                "Você já tem saldo para quitar as despesas atrasadas",
                "Faça o pagamento e dê baixa no sistema hoje",
                "Evite manter boletos vencidos para não gerar juros"
            ],
            "projected-deficit" => [
                "Reduza despesas não essenciais nesta semana",
                "Antecipe ou confirme receitas pendentes",
                "Defina teto diário de gastos até o fechamento"
            ],
            "overdue-incomes" => [
                "Confirme recebimentos em atraso hoje",
                "Atualize status das receitas já creditadas",
                "Evite contar com receita sem confirmação"
            ],
            "pending-risk" => [
                "Acompanhe os vencimentos dos próximos 5 dias",
                "Confirme entradas de receita pendente",
                "Priorize despesas essenciais"
            ],
            "no-income" => [
                "Registre a próxima receita prevista",
                "Revise despesas fixas do mês",
                "Monte reserva mínima para próximos vencimentos"
            ],
            "partial-coverage" => [
                "Ajuste despesas variáveis para ampliar cobertura",
                "Direcione entrada de receita para contas prioritárias",
                "Revise compras parceladas futuras"
            ],
            "pending-confirmation" => [
                "Marque receitas recebidas para corrigir o caixa",
                "Reveja pendências antigas",
                "Valide datas de recebimento recorrente"
            ],
            _ => []
        };
    }

    private static IReadOnlyList<string> BuildCashflowScoreBreakdown(
        decimal incomeReceived,
        decimal incomePending,
        decimal expenseTotal,
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal dueSoonExpenseAmount,
        decimal projectedBalance)
    {
        var breakdown = new List<string> { "Base: 100" };

        if (overdueExpensesCount > 0)
            breakdown.Add($"- {Math.Min(35, overdueExpensesCount * 12)} despesas atrasadas");

        if (overdueIncomesCount > 0)
            breakdown.Add($"- {Math.Min(20, overdueIncomesCount * 8)} receitas atrasadas");

        if (incomeReceived <= 0m && incomePending > 0m)
            breakdown.Add("- 20 sem receita recebida no mês");

        if (expenseTotal > 0m && incomeReceived > 0m && (incomeReceived / expenseTotal) < 0.6m)
            breakdown.Add("- 15 cobertura atual abaixo de 60%");

        if (dueSoonExpenseAmount > incomeReceived && dueSoonExpenseAmount > 0m)
            breakdown.Add("- 10 despesas de curto prazo acima das receitas recebidas");

        if (projectedBalance < 0m)
            breakdown.Add("- 20 saldo projetado negativo");

        return breakdown;
    }

    private static int CalculateHealthScore(
        decimal incomeReceived,
        decimal incomePending,
        decimal expenseTotal,
        int overdueExpensesCount,
        int overdueIncomesCount,
        decimal dueSoonExpenseAmount,
        decimal projectedBalance)
    {
        var score = 100;

        if (overdueExpensesCount > 0)
            score -= Math.Min(35, overdueExpensesCount * 12);

        if (overdueIncomesCount > 0)
            score -= Math.Min(20, overdueIncomesCount * 8);

        if (incomeReceived <= 0m && incomePending > 0m)
            score -= 20;

        if (expenseTotal > 0m && incomeReceived > 0m && (incomeReceived / expenseTotal) < 0.6m)
            score -= 15;

        if (dueSoonExpenseAmount > incomeReceived && dueSoonExpenseAmount > 0m)
            score -= 10;

        if (projectedBalance < 0m)
            score -= 20;

        return Math.Clamp(score, 0, 100);
    }

    private static DateOnly? EstimateRiskDay(
        DateOnly today,
        DateOnly competenceEnd,
        decimal currentIncome,
        IReadOnlyList<MoneyInstallment> openIncomes,
        IReadOnlyList<MoneyInstallment> openExpenses)
    {
        var dailyEvents = new SortedDictionary<DateOnly, decimal>();

        foreach (var income in openIncomes)
        {
            if (income.DueDate < today || income.DueDate > competenceEnd) continue;
            dailyEvents.TryAdd(income.DueDate, 0m);
            dailyEvents[income.DueDate] += income.Amount;
        }

        foreach (var expense in openExpenses)
        {
            if (expense.DueDate < today || expense.DueDate > competenceEnd) continue;
            dailyEvents.TryAdd(expense.DueDate, 0m);
            dailyEvents[expense.DueDate] -= expense.Amount;
        }

        var runningBalance = currentIncome;
        foreach (var (date, delta) in dailyEvents)
        {
            runningBalance += delta;
            if (runningBalance < 0m)
                return date;
        }

        return null;
    }
}
