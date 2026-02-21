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
