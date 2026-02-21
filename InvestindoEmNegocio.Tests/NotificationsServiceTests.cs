using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class NotificationsServiceTests
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

        var sut = BuildSut(notificationRepository: notificationRepository);

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
        var sut = BuildSut(profileRepository: profileRepository);

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
        var sut = BuildSut(notificationRepository: notificationRepository);

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
        var sut = BuildSut(notificationRepository: notificationRepository);

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

        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(1), 150m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        var plan = new MoneyPlan(userId, MoneyType.Income, "Salário", 150m, ScheduleType.OneTime, today, null, 1);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))!.SetValue(plan, installment.PlanId);
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildSut(
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

        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(1), 150m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        var plan = new MoneyPlan(userId, MoneyType.Income, "Salário", 150m, ScheduleType.OneTime, today, null, 1);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))!.SetValue(plan, installment.PlanId);
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildSut(
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

        var sut = BuildSut(profileRepository: profileRepository);

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

        var sut = BuildSut(
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

        var upcomingInstallment = new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(2), 100m);
        var overdueInstallment = new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(-2), 200m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([upcomingInstallment, overdueInstallment]);

        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Conta", 200m, ScheduleType.Recurring, today, null, 1);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))!.SetValue(expensePlan, upcomingInstallment.PlanId);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))!.SetValue(expensePlan, upcomingInstallment.PlanId);
        var expensePlan2 = new MoneyPlan(userId, MoneyType.Expense, "Aluguel", 500m, ScheduleType.Recurring, today, null, 1);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))!.SetValue(expensePlan2, overdueInstallment.PlanId);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([expensePlan, expensePlan2]);

        var notificationRepository = new Mock<IUserNotificationRepository>();
        notificationRepository
            .Setup(x => x.ExistsAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildSut(
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

        var sut = BuildSut(
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
        var sut = BuildSut(notificationRepository: notificationRepository);

        await sut.MarkAsReadAsync(userId, Guid.NewGuid());
        var readAt = notification.ReadAt;
        await sut.MarkAsReadAsync(userId, Guid.NewGuid());

        notification.ReadAt.Should().Be(readAt);
        notificationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static NotificationsService BuildSut(
        Mock<IUserNotificationRepository>? notificationRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IUserProfileRepository>? profileRepository = null,
        Mock<INotificationSettingsRepository>? settingsRepository = null,
        Mock<ICardRepository>? cardRepository = null,
        Mock<IGoalRepository>? goalRepository = null,
        Mock<IGoalContributionRepository>? goalContributionRepository = null)
    {
        return new NotificationsService(
            notificationRepository?.Object ?? Mock.Of<IUserNotificationRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            profileRepository?.Object ?? Mock.Of<IUserProfileRepository>(),
            settingsRepository?.Object ?? Mock.Of<INotificationSettingsRepository>(),
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            goalRepository?.Object ?? Mock.Of<IGoalRepository>(),
            goalContributionRepository?.Object ?? Mock.Of<IGoalContributionRepository>());
    }
}
