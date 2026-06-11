using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class SubscriptionManagementServiceTests
{
    [Fact]
    public async Task ChangeAsync_Should_Activate_Basic_Plan_And_Return_New_Session()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));
        var userSessionService = new UserSessionService(
            new RefreshTokenRepository(dbContext),
            jwt.Object);

        var sut = new SubscriptionManagementService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            userSessionService,
            Mock.Of<IStripeBillingGateway>(),
            Options.Create(new StripeOptions()));

        var result = await sut.ChangeAsync(user.Id, new("basic", "Monthly"));

        result.Current.PlanCode.Should().Be("basic");
        result.Current.Role.Should().Be("Basic");
        result.Session.Token.Should().Be("jwt-token");
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Basic);
        (await dbContext.UserSubscriptions.SingleAsync()).PlanCode.Should().Be("basic");
    }

    [Fact]
    public async Task CancelAsync_Should_Schedule_Cancellation_And_Keep_Access_Until_Cycle_End()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        await dbContext.Users.AddAsync(user);
        var renewsAt = DateTime.UtcNow.AddDays(20);
        var subscription = new UserSubscription(
            user.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL",
            DateTime.UtcNow.AddMonths(-1),
            renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        var userSessionService = new UserSessionService(
            new RefreshTokenRepository(dbContext),
            jwt.Object);
        var sut = new SubscriptionManagementService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            userSessionService,
            Mock.Of<IStripeBillingGateway>(),
            Options.Create(new StripeOptions()));

        jwt.Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await sut.CancelAsync(user.Id);

        result.Current.PlanCode.Should().Be("advanced");
        result.Current.AutoRenew.Should().BeFalse();
        result.Current.Status.Should().Be("Active");
        result.Current.RenewsAt.Should().BeCloseTo(renewsAt, TimeSpan.FromSeconds(1));
        result.Session.Token.Should().Be("jwt-token");
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Advanced);
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
