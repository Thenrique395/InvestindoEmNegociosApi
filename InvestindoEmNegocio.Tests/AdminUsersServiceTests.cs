using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AdminUsersServiceTests
{
    [Fact]
    public async Task UpdateRoleAsync_Should_Throw_When_Role_Is_Invalid()
    {
        var sut = new AdminUsersService(Mock.Of<IUserRepository>(), Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.UpdateRoleAsync(Guid.NewGuid(), "invalid", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Throw_When_User_Tries_To_Block_Himself()
    {
        var userId = Guid.NewGuid();
        var sut = new AdminUsersService(Mock.Of<IUserRepository>(), Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.UpdateStatusAsync(userId, false, userId, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Revoke_Sessions_When_Deactivating()
    {
        var currentUserId = Guid.NewGuid();
        var targetUser = new User("Tester", "tester@local", "hash");
        var previousTokenVersion = targetUser.TokenVersion;

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var userSessionService = new Mock<IUserSessionService>();
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), userSessionService.Object, Mock.Of<IAuditService>());

        await sut.UpdateStatusAsync(targetUser.Id, false, currentUserId, CancellationToken.None);

        targetUser.IsActive.Should().BeFalse();
        targetUser.TokenVersion.Should().BeGreaterThan(previousTokenVersion);
        userSessionService.Verify(x => x.RevokeActiveAsync(targetUser.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Not_Revoke_Sessions_When_Activating()
    {
        var currentUserId = Guid.NewGuid();
        var targetUser = new User("Tester", "tester@local", "hash");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var userSessionService = new Mock<IUserSessionService>();
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), userSessionService.Object, Mock.Of<IAuditService>());

        await sut.UpdateStatusAsync(targetUser.Id, true, currentUserId, CancellationToken.None);

        userSessionService.Verify(x => x.RevokeActiveAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_User_When_Found()
    {
        var currentUserId = Guid.NewGuid();
        var targetUser = new User("Tester", "tester@local", "hash");
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        await sut.DeleteAsync(targetUser.Id, currentUserId, CancellationToken.None);

        repository.Verify(x => x.Remove(targetUser), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRoleAsync_Should_Throw_When_User_Not_Found()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.UpdateRoleAsync(Guid.NewGuid(), "Admin", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Throw_When_User_Not_Found()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.UpdateStatusAsync(Guid.NewGuid(), true, Guid.NewGuid(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Current_User_Tries_To_Delete_Himself()
    {
        var userId = Guid.NewGuid();
        var sut = new AdminUsersService(Mock.Of<IUserRepository>(), Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.DeleteAsync(userId, userId, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ListAsync_Should_Map_Users()
    {
        var user = new User("Tester", "tester@local", "hash");
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        var result = await sut.ListAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(user.Id);
        result[0].Email.Should().Be("tester@local");
    }

    [Fact]
    public async Task UpdateRoleAsync_Should_Throw_AppProblem_When_Save_Fails()
    {
        var user = new User("Tester", "tester@local", "hash");
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db error"));
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.UpdateRoleAsync(user.Id, "Admin", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_AppProblem_When_Save_Fails()
    {
        var currentUserId = Guid.NewGuid();
        var user = new User("Tester", "tester@local", "hash");
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db error"));
        var sut = new AdminUsersService(repository.Object, Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        Func<Task> act = async () => await sut.DeleteAsync(user.Id, currentUserId, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ListFeaturesAsync_Should_Return_Matrix_With_Override()
    {
        var user = new User("Tester", "tester@local", "hash");
        user.SetRole(UserRole.Intermediate);
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var overridesRepository = new Mock<IUserFeatureOverrideRepository>();
        overridesRepository
            .Setup(x => x.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UserFeatureOverride(user.Id, "accounts.manage", false)
            ]);

        var sut = new AdminUsersService(userRepository.Object, Mock.Of<IUserSubscriptionRepository>(), overridesRepository.Object, Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>());

        var result = await sut.ListFeaturesAsync(user.Id, CancellationToken.None);

        result.Should().Contain(x => x.FeatureKey == "accounts.manage" && x.EffectiveEnabled == false && x.OverrideEnabled == false);
        result.Should().Contain(x => x.FeatureKey == "cards.read" && x.EffectiveEnabled);
    }

    [Fact]
    public async Task SetFeatureOverrideAsync_Should_Create_When_Not_Exists()
    {
        var user = new User("Tester", "tester@local", "hash");
        user.SetRole(UserRole.Basic);
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var overridesRepository = new Mock<IUserFeatureOverrideRepository>();
        overridesRepository
            .Setup(x => x.GetByUserAndFeatureAsync(user.Id, "investments.access", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserFeatureOverride?)null);
        overridesRepository
            .Setup(x => x.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UserFeatureOverride(user.Id, "investments.access", true)
            ]);

        var auditService = new Mock<IAuditService>();
        var sut = new AdminUsersService(userRepository.Object, Mock.Of<IUserSubscriptionRepository>(), overridesRepository.Object, Mock.Of<IUserSessionService>(), auditService.Object);

        var result = await sut.SetFeatureOverrideAsync(
            user.Id,
            "investments.access",
            true,
            Guid.NewGuid(),
            "127.0.0.1",
            "test-agent",
            CancellationToken.None);

        overridesRepository.Verify(x => x.AddAsync(It.IsAny<UserFeatureOverride>(), It.IsAny<CancellationToken>()), Times.Once);
        overridesRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(x => x.LogAsync(
            It.IsAny<Guid?>(),
            "SET_FEATURE_OVERRIDE",
            "UserFeatureOverride",
            user.Id.ToString(),
            "127.0.0.1",
            "test-agent",
            It.Is<string>(m => m.Contains("investments.access")),
            It.IsAny<CancellationToken>()), Times.Once);
        result.Should().Contain(x => x.FeatureKey == "investments.access" && x.EffectiveEnabled);
    }

    [Fact]
    public async Task ClearFeatureOverrideAsync_Should_Log_Audit_When_Override_Exists()
    {
        var user = new User("Tester", "tester@local", "hash");
        user.SetRole(UserRole.Basic);
        var existing = new UserFeatureOverride(user.Id, "investments.access", false);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var overridesRepository = new Mock<IUserFeatureOverrideRepository>();
        overridesRepository
            .Setup(x => x.GetByUserAndFeatureAsync(user.Id, "investments.access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        overridesRepository
            .Setup(x => x.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var auditService = new Mock<IAuditService>();
        var sut = new AdminUsersService(userRepository.Object, Mock.Of<IUserSubscriptionRepository>(), overridesRepository.Object, Mock.Of<IUserSessionService>(), auditService.Object);

        await sut.ClearFeatureOverrideAsync(
            user.Id,
            "investments.access",
            Guid.NewGuid(),
            "127.0.0.1",
            "test-agent",
            CancellationToken.None);

        overridesRepository.Verify(x => x.Remove(existing), Times.Once);
        overridesRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(x => x.LogAsync(
            It.IsAny<Guid?>(),
            "CLEAR_FEATURE_OVERRIDE",
            "UserFeatureOverride",
            user.Id.ToString(),
            "127.0.0.1",
            "test-agent",
            It.Is<string>(m => m.Contains("investments.access")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
