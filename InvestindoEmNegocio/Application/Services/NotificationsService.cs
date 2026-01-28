using System.Globalization;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class NotificationsService(
    IUserNotificationRepository notificationRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository planRepository,
    IUserProfileRepository profileRepository,
    INotificationSettingsRepository settingsRepository,
    ICardRepository cardRepository,
    IGoalRepository goalRepository,
    IGoalContributionRepository goalContributionRepository) : INotificationsService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default)
    {
        var items = await notificationRepository.ListByUserAsync(userId, unreadOnly, limit, cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<int> GenerateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
            return 0;

        if (!profile.NotifyInAppEnabled)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var settings = await settingsRepository.GetOrCreateAsync(cancellationToken);
        var toCreate = new List<UserNotification>();
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
                    if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                        continue;

                    var title = $"Receita recebe em {installment.DueDate:dd/MM}";
                    var message = $"{plan.Title} · R$ {installment.Amount.ToString("N2", culture)}";
                    toCreate.Add(new UserNotification(userId, NotificationKind.IncomeUpcoming, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
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
                    if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                        continue;

                    var title = $"Despesa vence em {installment.DueDate:dd/MM}";
                    var message = $"{plan.Title} · R$ {installment.Amount.ToString("N2", culture)}";
                    toCreate.Add(new UserNotification(userId, NotificationKind.ExpenseUpcoming, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
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
                    if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                        continue;

                    var title = "Despesa atrasada";
                    var message = $"{plan.Title} · Venceu em {installment.DueDate:dd/MM}";
                    toCreate.Add(new UserNotification(userId, NotificationKind.ExpenseOverdue, title, message, referenceKey, plan.Type, plan.Id, installment.Id, installment.DueDate));
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
                    if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                    {
                        var title = $"{card.Nickname} fecha hoje";
                        var message = $"Fatura fecha em {closeDate:dd/MM}.";
                        toCreate.Add(new UserNotification(userId, NotificationKind.CardClosingDay, title, message, referenceKey, null, null, null, closeDate));
                    }
                }

                if (settings.CardCloseSoonEnabled && settings.CardCloseDaysBefore > 0 && daysUntil > 0 && daysUntil <= settings.CardCloseDaysBefore)
                {
                    var referenceKey = $"card-close-soon:{card.Id}:{closeDate:yyyyMMdd}";
                    if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                    {
                        var title = $"Fatura fecha em {daysUntil} dias";
                        var message = $"{card.Nickname} · Fecha em {closeDate:dd/MM}";
                        toCreate.Add(new UserNotification(userId, NotificationKind.CardClosingSoon, title, message, referenceKey, null, null, null, closeDate));
                    }
                }
            }
        }

        if (settings.MonthCloseEnabled && IsLastDayOfMonth(today))
        {
            var referenceKey = $"month-close:{today:yyyyMM}";
            if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            {
                var title = "Fechamento do mês";
                var message = $"Hoje fecha o mês de {today.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture)}.";
                toCreate.Add(new UserNotification(userId, NotificationKind.MonthClosing, title, message, referenceKey, null, null, null, today));
            }
        }

        if (settings.MonthSummaryEnabled && today.Day == 1)
        {
            var referenceKey = $"month-summary:{today:yyyyMM}";
            if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            {
                var previousMonth = today.AddDays(-1);
                var title = "Resumo mensal disponível";
                var message = $"Resumo de {previousMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture)} pronto para revisão.";
                toCreate.Add(new UserNotification(userId, NotificationKind.MonthSummary, title, message, referenceKey, null, null, null, today));
            }
        }

        if (settings.GoalBelowExpectedEnabled || settings.GoalCompletedEnabled || settings.GoalInactivityEnabled)
        {
            var goals = await goalRepository.ListByUserAsync(userId, null, null, cancellationToken);
            foreach (var goal in goals)
            {
                if (settings.GoalCompletedEnabled && goal.Status == GoalStatus.Completed)
                {
                    var referenceKey = $"goal-completed:{goal.Id}";
                    if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                    {
                        var title = "Meta atingida";
                        var message = $"{goal.Title} · R$ {goal.TargetAmount.ToString("N2", culture)}";
                        toCreate.Add(new UserNotification(userId, NotificationKind.GoalCompleted, title, message, referenceKey, null, null, null, null));
                    }
                }

                if (settings.GoalBelowExpectedEnabled && goal.ExpectedMonthly > 0 && goal.Status is not GoalStatus.Completed and not GoalStatus.Canceled)
                {
                    var expected = goal.ExpectedMonthly * today.Month;
                    if (expected > 0 && goal.CurrentAmount < expected)
                    {
                        var referenceKey = $"goal-below:{goal.Id}:{today:yyyyMM}";
                        if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                        {
                            var title = "Meta abaixo do esperado";
                            var message = $"{goal.Title} · R$ {goal.CurrentAmount.ToString("N2", culture)} de R$ {expected.ToString("N2", culture)}";
                            toCreate.Add(new UserNotification(userId, NotificationKind.GoalBelowExpected, title, message, referenceKey, null, null, null, null));
                        }
                    }
                }

                if (settings.GoalInactivityEnabled && settings.GoalInactivityDays > 0 && goal.Status is not GoalStatus.Completed and not GoalStatus.Canceled)
                {
                    var contributions = await goalContributionRepository.ListByGoalAsync(goal.Id, userId, cancellationToken);
                    var lastDate = contributions.Count > 0
                        ? contributions.Max(c => c.Date)
                        : DateOnly.FromDateTime(goal.CreatedAt);
                    var daysSince = today.DayNumber - lastDate.DayNumber;
                    if (daysSince >= settings.GoalInactivityDays)
                    {
                        var referenceKey = $"goal-inactive:{goal.Id}:{today:yyyyMM}";
                        if (!await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
                        {
                            var title = "Meta sem movimentações";
                            var message = $"{goal.Title} · Sem aportes há {daysSince} dias.";
                            toCreate.Add(new UserNotification(userId, NotificationKind.GoalInactive, title, message, referenceKey, null, null, null, null));
                        }
                    }
                }
            }
        }

        if (toCreate.Count == 0)
            return 0;

        await notificationRepository.AddRangeAsync(toCreate, cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        return toCreate.Count;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var item = await notificationRepository.GetByIdAsync(notificationId, userId, cancellationToken);
        if (item is null)
            return;

        item.MarkAsRead();
        await notificationRepository.SaveChangesAsync(cancellationToken);
    }

    private static NotificationDto ToDto(UserNotification notification) =>
        new(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Kind,
            notification.MoneyType,
            notification.DueDate,
            notification.CreatedAt,
            notification.ReadAt);

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
}
