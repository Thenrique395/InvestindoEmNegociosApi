using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;
using System.Text.Json;

namespace InvestindoEmNegocio.Tests;

public class NotificationApplicationServicesTests
{
    [Fact]
    public async Task ListAsync_Should_Map_Notifications_To_Dto()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = new UserNotification(userId, NotificationKind.ExpenseUpcoming, "Titulo 1", "Mensagem 1", "ref-1", MoneyType.Expense, null, null, DateOnly.FromDateTime(now));
        var second = new UserNotification(userId, NotificationKind.MonthSummary, "Titulo 2", "Mensagem 2", "ref-2");
        second.MarkAsRead();

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ListByUserAsync(userId, true, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);

        var sut = BuildQuerySut(notificationRepository: notificationRepository);

        var result = await sut.ListAsync(userId, unreadOnly: true, limit: 10);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Titulo 1");
        result[0].MoneyType.Should().Be(MoneyType.Expense);
        result[1].ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_Should_Return_Zero_When_Profile_Not_Found()
    {
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var sut = BuildGenerationSut(profileRepository: profileRepository);

        var generated = await sut.GenerateAsync(Guid.NewGuid());

        generated.Should().Be(0);
    }

    [Fact]
    public async Task MarkAsReadAsync_Should_Not_Save_When_Notification_Not_Found()
    {
        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);
        var sut = BuildCommandSut(notificationRepository: notificationRepository);

        await sut.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid());

        notificationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsReadAsync_Should_Save_When_Notification_Exists()
    {
        var userId = Guid.NewGuid();
        var notification = new UserNotification(
            userId,
            NotificationKind.IncomeUpcoming,
            "Receita",
            "Mensagem",
            "ref-1");
        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        var sut = BuildCommandSut(notificationRepository: notificationRepository);

        await sut.MarkAsReadAsync(userId, Guid.NewGuid());

        notification.ReadAt.Should().NotBeNull();
        notificationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Income_Upcoming_Notification()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: true,
                incomeDaysBefore: 3,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: false,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var plan = new MoneyPlan(userId, MoneyType.Income, "Salário", 150m, ScheduleType.OneTime, today, null, 1);
        var installment = new MoneyInstallment(plan.Id, userId, 1, today.AddDays(1), 150m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        notificationRepository.Verify(x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n => n.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        notificationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Not_Create_Duplicate_Notification_When_Reference_Exists()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(true, 3, false, 0, false, false, 0, false, false, false, false, false, false, 0));

        var plan = new MoneyPlan(userId, MoneyType.Income, "Salário", 150m, ScheduleType.OneTime, today, null, 1);
        var installment = new MoneyInstallment(plan.Id, userId, 1, today.AddDays(1), 150m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(0);
        notificationRepository.Verify(x => x.AddRangeAsync(It.IsAny<IReadOnlyList<UserNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_Should_Return_Zero_When_InApp_Notifications_Are_Disabled()
    {
        var userId = Guid.NewGuid();
        var profile = new UserProfile(userId, "User", "123", "5511999999999", null);
        profile.SetNotificationPreferences(upcomingEnabled: true, overdueEnabled: true, emailEnabled: true, inAppEnabled: false, daysBeforeDue: 3);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var sut = BuildGenerationSut(profileRepository: profileRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Card_Closing_Day_Notification()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: true,
                monthCloseEnabled: false,
                monthSummaryEnabled: false,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Card(userId, 1, "User", "Cartão principal", "1234", null, 1000m, today.Day, today.Day == 28 ? 27 : 28)]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository,
            cardRepository: cardRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n => n.Any(i => i.Kind == NotificationKind.CardClosingDay)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Expense_Upcoming_And_Overdue_Notifications()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: true,
                expenseDaysBefore: 3,
                expenseOverdueEnabled: true,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: false,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Conta", 200m, ScheduleType.Recurring, today, FrequencyType.Monthly, null);
        var expensePlan2 = new MoneyPlan(userId, MoneyType.Expense, "Aluguel", 500m, ScheduleType.Recurring, today, FrequencyType.Monthly, null);
        var upcomingInstallment = new MoneyInstallment(expensePlan.Id, userId, 1, today.AddDays(2), 100m);
        var overdueInstallment = new MoneyInstallment(expensePlan2.Id, userId, 1, today.AddDays(-2), 200m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([upcomingInstallment, overdueInstallment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expensePlan, expensePlan2]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(2);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n =>
                n.Any(i => i.Kind == NotificationKind.ExpenseUpcoming) &&
                n.Any(i => i.Kind == NotificationKind.ExpenseOverdue)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Goal_Notifications_For_Completed_Below_And_Inactive()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: false,
                goalBelowExpectedEnabled: true,
                goalCompletedEnabled: true,
                goalInactivityEnabled: true,
                goalInactivityDays: 5));

        var completed = new Goal(userId, "Meta 1", 1000m, today.Year, status: GoalStatus.Completed, currentAmount: 1000m, expectedMonthly: 50m);
        var below = new Goal(userId, "Meta 2", 1000m, today.Year, status: GoalStatus.InProgress, currentAmount: 10m, expectedMonthly: 200m);
        var inactive = new Goal(userId, "Meta 3", 1000m, today.Year, status: GoalStatus.InProgress, currentAmount: 100m, expectedMonthly: 10m);

        var goalRepository = new Mock<IGoalRepository>();
        goalRepository.Setup(x => x.ListByUserAsync(userId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([completed, below, inactive]);

        var goalContributionRepository = new Mock<IGoalContributionRepository>();
        goalContributionRepository.Setup(x => x.ListByGoalAsync(inactive.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GoalContribution(inactive.Id, userId, 50m, today.AddDays(-10))]);
        goalContributionRepository.Setup(x => x.ListByGoalAsync(completed.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        goalContributionRepository.Setup(x => x.ListByGoalAsync(below.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository.Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository,
            goalRepository: goalRepository,
            goalContributionRepository: goalContributionRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().BeGreaterOrEqualTo(3);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n =>
                n.Any(i => i.Kind == NotificationKind.GoalCompleted) &&
                n.Any(i => i.Kind == NotificationKind.GoalBelowExpected) &&
                n.Any(i => i.Kind == NotificationKind.GoalInactive)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_Should_Be_Idempotent_When_Called_Twice()
    {
        var userId = Guid.NewGuid();
        var notification = new UserNotification(userId, NotificationKind.IncomeUpcoming, "Receita", "Mensagem", "ref-1");
        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        var sut = BuildCommandSut(notificationRepository: notificationRepository);

        await sut.MarkAsReadAsync(userId, Guid.NewGuid());
        var readAt = notification.ReadAt;
        await sut.MarkAsReadAsync(userId, Guid.NewGuid());

        notification.ReadAt.Should().Be(readAt);
        notificationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_CashflowInsight_With_Critical_Overdue_Expenses()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "81999999999", null, carryOverDay: CashflowCarryOverDay(today)));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: true,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Aluguel", 900m, ScheduleType.OneTime, today, null, 1);
        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Salário", 1000m, ScheduleType.OneTime, today, null, 1);
        var overdueExpense = new MoneyInstallment(expensePlan.Id, userId, 1, today.AddDays(-1), 900m);
        var pendingIncome = new MoneyInstallment(incomePlan.Id, userId, 1, today.AddDays(5), 1000m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([overdueExpense, pendingIncome]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expensePlan, incomePlan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => !key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n =>
                n.Count == 1 &&
                n[0].Kind == NotificationKind.CashflowInsight &&
                n[0].Title == "Ação imediata: despesas atrasadas" &&
                n[0].Message.Contains("saúde financeira", StringComparison.OrdinalIgnoreCase) &&
                n[0].Message.Contains("Atrasos: 1 despesa(s) e 0 receita(s).")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_CashflowInsight_With_Projected_Deficit_And_Risk_Day()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "81999999999", null, carryOverDay: today.Day));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: true,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Salário", 100m, ScheduleType.OneTime, today, null, 1);
        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Cartão", 600m, ScheduleType.OneTime, today, null, 1);
        var openIncomePlan = new MoneyPlan(userId, MoneyType.Income, "Freela", 100m, ScheduleType.OneTime, today, null, 1);
        var receivedIncome = new MoneyInstallment(incomePlan.Id, userId, 1, today, 100m);
        SetInstallmentStatus(receivedIncome, InstallmentStatus.Paid);
        var openExpense = new MoneyInstallment(expensePlan.Id, userId, 1, today.AddDays(1), 600m);
        var openIncome = new MoneyInstallment(openIncomePlan.Id, userId, 1, today.AddDays(10), 100m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([receivedIncome, openExpense, openIncome]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([incomePlan, expensePlan, openIncomePlan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => !key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n =>
                n.Count == 1 &&
                n[0].Kind == NotificationKind.CashflowInsight &&
                n[0].Title == "Risco de fechar o mês no negativo" &&
                n[0].Message.Contains("Dia de risco:", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Warning_Insight_When_Overdue_Expense_Is_Covered_By_Received_Income()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "81999999999", null, carryOverDay: CashflowCarryOverDay(today)));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: true,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Salário", 1000m, ScheduleType.OneTime, today, null, 1);
        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Condomínio", 300m, ScheduleType.OneTime, today, null, 1);
        var receivedIncome = new MoneyInstallment(incomePlan.Id, userId, 1, today, 1000m);
        SetInstallmentStatus(receivedIncome, InstallmentStatus.Paid);
        var overdueExpense = new MoneyInstallment(expensePlan.Id, userId, 1, today.AddDays(-1), 300m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([receivedIncome, overdueExpense]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([incomePlan, expensePlan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => !key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        notificationRepository.Verify(
            x => x.AddRangeAsync(It.Is<IReadOnlyList<UserNotification>>(n =>
                n.Count == 1 &&
                n[0].Kind == NotificationKind.CashflowInsight &&
                n[0].Title == "Despesas vencidas com cobertura disponível" &&
                n[0].PayloadJson!.Contains("\"priority\":\"warning\"", StringComparison.OrdinalIgnoreCase) &&
                n[0].PayloadJson!.Contains("\"overdueExpensesCovered\":true", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Should_Include_Prioritized_Recommendations_And_ReasonCodes_In_Cashflow_Payload()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "81999999999", null, carryOverDay: CashflowCarryOverDay(today)));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: false,
                incomeDaysBefore: 0,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: true,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Aluguel", 900m, ScheduleType.OneTime, today, null, 1);
        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Salário", 500m, ScheduleType.OneTime, today, null, 1);
        var overdueExpense = new MoneyInstallment(expensePlan.Id, userId, 1, today.AddDays(-1), 900m);
        var pendingIncome = new MoneyInstallment(incomePlan.Id, userId, 1, today.AddDays(2), 500m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([overdueExpense, pendingIncome]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expensePlan, incomePlan]);

        IReadOnlyList<UserNotification>? createdNotifications = null;
        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.Is<string>(key => !key.StartsWith("month-summary:")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        notificationRepository
            .Setup(x => x.AddRangeAsync(It.IsAny<IReadOnlyList<UserNotification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<UserNotification>, CancellationToken>((items, _) => createdNotifications = items.ToList());

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(1);
        createdNotifications.Should().NotBeNull();
        createdNotifications!.Should().ContainSingle(n => n.Kind == NotificationKind.CashflowInsight);

        var payloadJson = createdNotifications![0].PayloadJson;
        payloadJson.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(payloadJson!);
        var root = doc.RootElement;
        root.GetProperty("priority").GetString().Should().Be("critical");

        var reasonCodes = root.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()).ToList();
        reasonCodes.Should().Contain("overdue_expenses_uncovered");

        var recommendations = root.GetProperty("recommendations").EnumerateArray().ToList();
        recommendations.Should().NotBeEmpty();
        recommendations[0].GetProperty("route").GetString().Should().Be("/expenses");
        recommendations[0].GetProperty("actionLabel").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateAsync_Should_Skip_Create_When_ListReferenceKeys_Returns_All_Candidates()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile(userId, "User", "123", "5511999999999", null));

        var settingsRepository = new Mock<INotificationSettingsRepository>();
        settingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSettings(
                incomeUpcomingEnabled: true,
                incomeDaysBefore: 3,
                expenseUpcomingEnabled: false,
                expenseDaysBefore: 0,
                expenseOverdueEnabled: false,
                cardCloseSoonEnabled: false,
                cardCloseDaysBefore: 0,
                cardCloseDayEnabled: false,
                monthCloseEnabled: false,
                monthSummaryEnabled: false,
                goalBelowExpectedEnabled: false,
                goalCompletedEnabled: false,
                goalInactivityEnabled: false,
                goalInactivityDays: 0));

        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Receita", 500m, ScheduleType.OneTime, today, null, 1);
        var installment = new MoneyInstallment(incomePlan.Id, userId, 1, today.AddDays(2), 500m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([incomePlan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ListReferenceKeysAsync(userId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, IEnumerable<string>, CancellationToken>((_, keys, _) =>
                Task.FromResult(new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase)));

        var sut = BuildGenerationSut(
            notificationRepository: notificationRepository,
            installmentRepository: installmentRepository,
            planRepository: planRepository,
            profileRepository: profileRepository,
            settingsRepository: settingsRepository);

        var generated = await sut.GenerateAsync(userId, CancellationToken.None);

        generated.Should().Be(0);
        notificationRepository.Verify(x => x.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        notificationRepository.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static NotificationQueryService BuildQuerySut(
        Mock<IUserNotificationRepository>? notificationRepository = null)
    {
        return new NotificationQueryService(
            notificationRepository?.Object ?? Mock.Of<IUserNotificationRepository>());
    }

    private static NotificationCommandService BuildCommandSut(
        Mock<IUserNotificationRepository>? notificationRepository = null)
    {
        return new NotificationCommandService(
            notificationRepository?.Object ?? Mock.Of<IUserNotificationRepository>());
    }

    private static NotificationGenerationService BuildGenerationSut(
        Mock<IUserNotificationRepository>? notificationRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IUserProfileRepository>? profileRepository = null,
        Mock<INotificationSettingsRepository>? settingsRepository = null,
        Mock<ICardRepository>? cardRepository = null,
        Mock<IGoalRepository>? goalRepository = null,
        Mock<IGoalContributionRepository>? goalContributionRepository = null)
    {
        return new NotificationGenerationService(
            notificationRepository?.Object ?? Mock.Of<IUserNotificationRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            profileRepository?.Object ?? Mock.Of<IUserProfileRepository>(),
            settingsRepository?.Object ?? Mock.Of<INotificationSettingsRepository>(),
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            goalRepository?.Object ?? Mock.Of<IGoalRepository>(),
            goalContributionRepository?.Object ?? Mock.Of<IGoalContributionRepository>());
    }

    private static void SetInstallmentStatus(MoneyInstallment installment, InstallmentStatus status)
    {
        installment.RestoreStatus(status);
    }

    private static int CashflowCarryOverDay(DateOnly today)
    {
        return today.Day == 1 ? 28 : 1;
    }
}
