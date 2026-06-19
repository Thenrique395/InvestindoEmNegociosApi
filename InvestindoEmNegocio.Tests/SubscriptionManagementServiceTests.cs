using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class SubscriptionManagementServiceTests
{
    private static SubscriptionManagementService CreateSut(InvestDbContext dbContext, IJwtTokenGenerator? jwt = null)
    {
        var jwtMock = jwt ?? Mock.Of<IJwtTokenGenerator>();
        var userSessionService = new UserSessionService(new RefreshTokenRepository(dbContext), jwtMock, Mock.Of<ILogger<UserSessionService>>());
        return new SubscriptionManagementService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            Mock.Of<IBillingCheckoutRepository>(),
            userSessionService,
            Mock.Of<IStripeBillingGateway>(),
            Options.Create(new StripeOptions()));
    }

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

        var result = await CreateSut(dbContext, jwt.Object).ChangeAsync(user.Id, new("basic", "Monthly"));

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
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await CreateSut(dbContext, jwt.Object).CancelAsync(user.Id);

        result.Current.PlanCode.Should().Be("advanced");
        result.Current.AutoRenew.Should().BeFalse();
        result.Current.Status.Should().Be("Active");
        result.Current.RenewsAt.Should().BeCloseTo(renewsAt, TimeSpan.FromSeconds(1));
        result.Session.Token.Should().Be("jwt-token");
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RequestRefundAsync_Should_Downgrade_User_And_Mark_Refunded()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "refund@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await CreateSut(dbContext, jwt.Object).RequestRefundAsync(user.Id);

        result.Current.PlanCode.Should().Be("advanced");
        result.Current.Status.Should().Be("Refunded");
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Basic);
        (await dbContext.UserSubscriptions.SingleAsync()).Status.Should().Be(UserSubscriptionStatus.Refunded);
    }

    [Fact]
    public async Task RequestRefundAsync_Should_Throw_When_Grace_Period_Expired()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "refund-expired@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        // StartedAt 8 days ago → beyond the 7-day grace period
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddDays(-8), DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddDays(-8), DateTime.UtcNow.AddMonths(1));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        await sut.Invoking(x => x.RequestRefundAsync(user.Id))
            .Should().ThrowAsync<AppProblemException>()
            .WithMessage("*arrependimento*");
    }

    [Fact]
    public async Task RequestTrialAsync_Should_Activate_Trial_And_Promote_User()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "trial@teste.com", "hash");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        await CreateSut(dbContext, jwt.Object).RequestTrialAsync(user.Id);

        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Advanced);
        (await dbContext.Users.SingleAsync()).TrialUsedAt.Should().NotBeNull();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        storedSubscription.IsTrial.Should().BeTrue();
        storedSubscription.PlanCode.Should().Be("advanced");
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
    }

    [Fact]
    public async Task RequestTrialAsync_Should_Throw_When_Trial_Already_Used()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "trial-used@teste.com", "hash");
        user.MarkTrialUsed(DateTime.UtcNow.AddDays(-10));
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        await sut.Invoking(x => x.RequestTrialAsync(user.Id))
            .Should().ThrowAsync<AppProblemException>()
            .WithMessage("*período de teste*");
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
