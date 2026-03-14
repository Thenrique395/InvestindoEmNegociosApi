using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_Should_Return_Default_When_Profile_Not_Found()
    {
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var sut = new PreferencesService(
            profileRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IRefreshTokenRepository>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IInvestDbContext>());

        var result = await sut.GetAsync(Guid.NewGuid());

        result.Currency.Should().Be("BRL");
        result.Locales.Should().ContainSingle().Which.Should().Be("pt-BR");
        result.Notifications.UpcomingEnabled.Should().BeTrue();
        result.Notifications.OverdueEnabled.Should().BeTrue();
        result.Notifications.InAppEnabled.Should().BeTrue();
        result.Notifications.EmailEnabled.Should().BeFalse();
        result.Notifications.DaysBeforeDue.Should().Be(3);
    }

    [Fact]
    public async Task UpdateAsync_Should_Create_Profile_When_Not_Found()
    {
        var userId = Guid.NewGuid();
        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var sut = new PreferencesService(
            profileRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IRefreshTokenRepository>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IInvestDbContext>());

        var request = new UpdatePreferencesRequest(
            "USD",
            ["en-US"],
            new NotificationPreferencesDto(
                UpcomingEnabled: false,
                OverdueEnabled: true,
                InAppEnabled: false,
                EmailEnabled: true,
                DaysBeforeDue: 5));

        var result = await sut.UpdateAsync(userId, request);

        result.Currency.Should().Be("USD");
        result.Locales.Should().Equal("en-US");
        result.Notifications.Should().BeEquivalentTo(request.Notifications);
        profileRepository.Verify(x => x.AddAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        profileRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteOwnAccountAsync_Should_Remove_User_And_Data_When_Request_Is_Valid()
    {
        await using var dbContext = CreateDbContext();
        var userRepository = new UserRepository(dbContext);
        var profileRepository = new UserProfileRepository(dbContext);
        var user = new User("Teste", "teste@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha@123"));
        var userId = user.Id;

        await dbContext.Users.AddAsync(user);
        await dbContext.UserProfiles.AddAsync(new UserProfile(userId, "Teste", "12345678901", "(81) 99999-9999", null));
        await dbContext.Categories.AddAsync(new Category(userId, "Moradia", MoneyType.Expense));
        await dbContext.Accounts.AddAsync(new Account(userId, "Conta principal", AccountType.Checking, 1000m));
        await dbContext.RefreshTokens.AddAsync(new RefreshToken(userId, "refresh-hash", DateTime.UtcNow.AddDays(7)));
        await dbContext.PasswordResetTokens.AddAsync(new PasswordResetToken(userId, "reset-hash", DateTime.UtcNow.AddHours(1)));
        await dbContext.AuditLogs.AddAsync(new AuditLog(userId, "account.delete.requested", "User", userId.ToString(), null, null, null));
        await dbContext.SaveChangesAsync();

        var sut = new PreferencesService(profileRepository, userRepository, new RefreshTokenRepository(dbContext), Mock.Of<IAuditService>(), dbContext);
        var request = new DeleteOwnAccountRequest("Senha@123", "EXCLUIR");

        await sut.DeleteOwnAccountAsync(userId, request);

        (await dbContext.Users.CountAsync(x => x.Id == userId)).Should().Be(0);
        (await dbContext.UserProfiles.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await dbContext.Categories.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await dbContext.Accounts.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await dbContext.RefreshTokens.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await dbContext.PasswordResetTokens.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await dbContext.AuditLogs.CountAsync(x => x.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task DeleteOwnAccountAsync_Should_Throw_When_Password_Is_Invalid()
    {
        await using var dbContext = CreateDbContext();
        var userRepository = new UserRepository(dbContext);
        var profileRepository = new UserProfileRepository(dbContext);
        var user = new User("Teste", "teste@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha@123"));
        var userId = user.Id;

        await dbContext.Users.AddAsync(user);
        await dbContext.UserProfiles.AddAsync(new UserProfile(userId, "Teste", "12345678901", "(81) 99999-9999", null));
        await dbContext.SaveChangesAsync();

        var sut = new PreferencesService(profileRepository, userRepository, new RefreshTokenRepository(dbContext), Mock.Of<IAuditService>(), dbContext);
        var request = new DeleteOwnAccountRequest("SenhaErrada", "EXCLUIR");

        var act = async () => await sut.DeleteOwnAccountAsync(userId, request);

        await act.Should().ThrowAsync<AppProblemException>()
            .Where(ex => ex.Title == "Senha inválida");
        (await dbContext.Users.CountAsync(x => x.Id == userId)).Should().Be(1);
    }

    [Fact]
    public async Task GetPrivacySummaryAsync_Should_Report_Runtime_Privacy_Controls()
    {
        await using var dbContext = CreateDbContext();
        var userRepository = new UserRepository(dbContext);
        var profileRepository = new UserProfileRepository(dbContext);
        var user = new User("Teste", "teste@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha@123"));
        var userId = user.Id;

        await dbContext.Users.AddAsync(user);
        await dbContext.RefreshTokens.AddRangeAsync(
            new RefreshToken(userId, "refresh-1", DateTime.UtcNow.AddHours(2)),
            new RefreshToken(userId, "refresh-2", DateTime.UtcNow.AddHours(-1)));
        await dbContext.PasswordResetTokens.AddAsync(new PasswordResetToken(userId, "reset-1", DateTime.UtcNow.AddHours(2)));
        await dbContext.AuditLogs.AddRangeAsync(
            new AuditLog(userId, "login", "Auth", null, null, null, null),
            new AuditLog(userId, "export", "DataPortability", null, null, null, null));
        await dbContext.SaveChangesAsync();

        var sut = new PreferencesService(profileRepository, userRepository, new RefreshTokenRepository(dbContext), Mock.Of<IAuditService>(), dbContext);

        var result = await sut.GetPrivacySummaryAsync(userId);

        result.ActiveSessions.Should().Be(1);
        result.PendingPasswordResetRequests.Should().Be(1);
        result.AuditEntries.Should().Be(2);
        result.SelfServiceDeletionEnabled.Should().BeTrue();
        result.DeletionScope.Should().Contain("tokens de sessão, reset de senha e trilha de auditoria");
    }

    [Fact]
    public async Task RevokeOwnSessionsAsync_Should_Revoke_Only_Active_Sessions()
    {
        await using var dbContext = CreateDbContext();
        var userRepository = new UserRepository(dbContext);
        var profileRepository = new UserProfileRepository(dbContext);
        var refreshTokenRepository = new RefreshTokenRepository(dbContext);
        var user = new User("Teste", "teste@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha@123"));
        var userId = user.Id;

        await dbContext.Users.AddAsync(user);
        await dbContext.RefreshTokens.AddRangeAsync(
            new RefreshToken(userId, "refresh-1", DateTime.UtcNow.AddHours(2)),
            new RefreshToken(userId, "refresh-2", DateTime.UtcNow.AddHours(2)),
            new RefreshToken(userId, "refresh-3", DateTime.UtcNow.AddHours(-2)));
        await dbContext.SaveChangesAsync();

        var sut = new PreferencesService(profileRepository, userRepository, refreshTokenRepository, Mock.Of<IAuditService>(), dbContext);

        var result = await sut.RevokeOwnSessionsAsync(userId);

        result.RevokedSessions.Should().Be(2);
        (await dbContext.RefreshTokens.CountAsync(x => x.UserId == userId && x.RevokedAt.HasValue)).Should().Be(2);
    }

    [Fact]
    public async Task GetSecuritySummaryAsync_Should_Report_Login_State_And_Last_Login()
    {
        await using var dbContext = CreateDbContext();
        var userRepository = new UserRepository(dbContext);
        var profileRepository = new UserProfileRepository(dbContext);
        var refreshTokenRepository = new RefreshTokenRepository(dbContext);
        var user = new User("Teste", "teste@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha@123"));
        user.RegisterFailedLogin(DateTime.UtcNow, 5, TimeSpan.FromMinutes(15));
        user.UpdateLastLogin(DateTime.UtcNow.AddMinutes(-10));
        var userId = user.Id;

        await dbContext.Users.AddAsync(user);
        await dbContext.RefreshTokens.AddAsync(new RefreshToken(userId, "refresh-1", DateTime.UtcNow.AddHours(2)));
        await dbContext.SaveChangesAsync();

        var sut = new PreferencesService(profileRepository, userRepository, refreshTokenRepository, Mock.Of<IAuditService>(), dbContext);

        var result = await sut.GetSecuritySummaryAsync(userId);

        result.ActiveSessions.Should().Be(1);
        result.FailedLoginAttempts.Should().Be(1);
        result.LastLoginAt.Should().NotBeNull();
        result.Controls.Should().Contain("revogação de sessões ativas pelo próprio usuário");
    }

    private static InvestDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new InvestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
