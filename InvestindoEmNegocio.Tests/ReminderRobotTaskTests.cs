using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class ReminderRobotTaskTests
{
    [Fact]
    public async Task RunAsync_Should_Send_Email_When_User_Has_Email_Enabled_And_New_Notifications()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        var profile = new UserProfile(user.Id, "Teste", "12345678901", "(81) 99999-9999", null);
        profile.SetNotificationPreferences(upcomingEnabled: true, overdueEnabled: true, emailEnabled: true, inAppEnabled: true, daysBeforeDue: 3);

        var usersRepo = new Mock<IUserRepository>();
        usersRepo.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([user]);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(x => x.ListByUserAsync(user.Id, true, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UserNotification(
                    user.Id,
                    NotificationKind.ExpenseUpcoming,
                    "Despesa vence amanhã",
                    "Conta de luz vence amanhã.",
                    "ref-1",
                    MoneyType.Expense)
            ]);

        var notificationsService = new Mock<INotificationsService>();
        notificationsService.Setup(x => x.GenerateAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var emailSender = new Mock<IEmailSender>();

        var sut = new ReminderRobotTask(
            usersRepo.Object,
            profileRepo.Object,
            notifRepo.Object,
            notificationsService.Object,
            emailSender.Object,
            Mock.Of<ILogger<ReminderRobotTask>>());

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(1);
        result.EmailsAttempted.Should().Be(1);
        result.EmailsSent.Should().Be(1);
        result.EmailsFailed.Should().Be(0);
        emailSender.Verify(x => x.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Should_Not_Send_Email_When_Profile_Email_Is_Disabled()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        var profile = new UserProfile(user.Id, "Teste", "12345678901", "(81) 99999-9999", null);
        profile.SetNotificationPreferences(upcomingEnabled: true, overdueEnabled: true, emailEnabled: false, inAppEnabled: true, daysBeforeDue: 3);

        var usersRepo = new Mock<IUserRepository>();
        usersRepo.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([user]);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(x => x.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification>());

        var notificationsService = new Mock<INotificationsService>();
        notificationsService.Setup(x => x.GenerateAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var emailSender = new Mock<IEmailSender>();

        var sut = new ReminderRobotTask(
            usersRepo.Object,
            profileRepo.Object,
            notifRepo.Object,
            notificationsService.Object,
            emailSender.Object,
            Mock.Of<ILogger<ReminderRobotTask>>());

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(2);
        result.EmailsAttempted.Should().Be(0);
        result.EmailsSent.Should().Be(0);
        result.EmailsFailed.Should().Be(0);
        emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
