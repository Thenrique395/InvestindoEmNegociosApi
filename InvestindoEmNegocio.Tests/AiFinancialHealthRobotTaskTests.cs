using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AiFinancialHealthRobotTaskTests
{
    private static AiFinancialHealthResponse BuildHealth(string overallStatus) =>
        new(new DateOnly(2026, 6, 29), overallStatus, "Resumo de teste.", [
            new AiHealthAreaVerdict("cashflow", overallStatus, "explicação"),
            new AiHealthAreaVerdict("divida", "ok", "explicação"),
            new AiHealthAreaVerdict("patrimonio", "ok", "explicação")
        ], GeneratedByAi: true);

    private static AiFinancialHealthRobotTask CreateSut(
        IEnumerable<User> users,
        Mock<IUserProfileRepository> profileRepo,
        Mock<IUserNotificationRepository> notifRepo,
        Mock<IAiFinancialHealthService> healthService)
    {
        var usersRepo = new Mock<IUserRepository>();
        usersRepo.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users.ToList());
        return new AiFinancialHealthRobotTask(usersRepo.Object, profileRepo.Object, notifRepo.Object, healthService.Object);
    }

    [Fact]
    public async Task RunAsync_Should_Create_Notification_When_Overall_Status_Is_Not_Ok()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        user.SetRole(UserRole.Intermediate);
        var profile = new UserProfile(user.Id);
        profile.SetNotificationPreferences(true, true, false, inAppEnabled: true, daysBeforeDue: 3);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(x => x.ExistsAsync(user.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var healthService = new Mock<IAiFinancialHealthService>();
        healthService.Setup(x => x.GetHealthAsync(user.Id, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildHealth("warning"));

        var sut = CreateSut([user], profileRepo, notifRepo, healthService);

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(1);
        notifRepo.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<UserNotification>>(n => n.Count() == 1 && n.First().Kind == NotificationKind.AiHealthAlert), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Should_Not_Create_Notification_When_Overall_Status_Is_Ok()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        user.SetRole(UserRole.Intermediate);
        var profile = new UserProfile(user.Id);
        profile.SetNotificationPreferences(true, true, false, inAppEnabled: true, daysBeforeDue: 3);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();

        var healthService = new Mock<IAiFinancialHealthService>();
        healthService.Setup(x => x.GetHealthAsync(user.Id, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildHealth("ok"));

        var sut = CreateSut([user], profileRepo, notifRepo, healthService);

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(0);
        notifRepo.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Should_Respect_NotifyInAppEnabled_False()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        user.SetRole(UserRole.Intermediate);
        var profile = new UserProfile(user.Id);
        profile.SetNotificationPreferences(true, true, false, inAppEnabled: false, daysBeforeDue: 3);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();

        var healthService = new Mock<IAiFinancialHealthService>();
        healthService.Setup(x => x.GetHealthAsync(user.Id, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildHealth("critical"));

        var sut = CreateSut([user], profileRepo, notifRepo, healthService);

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(0);
        notifRepo.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Should_Ignore_Users_Without_Feature_Access()
    {
        var basicUser = new User("Basic", "basic@email.com", "hash");

        var usersRepo = new Mock<IUserRepository>();
        usersRepo.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([basicUser]);

        var profileRepo = new Mock<IUserProfileRepository>();
        var notifRepo = new Mock<IUserNotificationRepository>();
        var healthService = new Mock<IAiFinancialHealthService>();

        var sut = new AiFinancialHealthRobotTask(usersRepo.Object, profileRepo.Object, notifRepo.Object, healthService.Object);

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(0);
        result.ZeroItemsReasonCode.Should().Be("NO_ELIGIBLE_USERS");
        healthService.Verify(x => x.GetHealthAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_Should_Not_Duplicate_Notification_When_Already_Exists_For_Today()
    {
        var user = new User("Teste", "teste@email.com", "hash");
        user.SetRole(UserRole.Intermediate);
        var profile = new UserProfile(user.Id);
        profile.SetNotificationPreferences(true, true, false, inAppEnabled: true, daysBeforeDue: 3);

        var profileRepo = new Mock<IUserProfileRepository>();
        profileRepo.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(x => x.ExistsAsync(user.Id, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var healthService = new Mock<IAiFinancialHealthService>();
        healthService.Setup(x => x.GetHealthAsync(user.Id, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildHealth("warning"));

        var sut = CreateSut([user], profileRepo, notifRepo, healthService);

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(0);
        notifRepo.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
