using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

public class SubscriptionExpirationRobotTaskTests
{
    [Fact]
    public async Task RunAsync_Should_Downgrade_Subscriptions_Past_RenewsAt_With_AutoRenew_Disabled()
    {
        await using var dbContext = CreateDbContext();

        var expiredUser = new User("Expirado", "expirado@teste.com", "hash");
        expiredUser.SetRole(UserRole.Advanced);
        var expiredRenewsAt = DateTime.UtcNow.AddDays(-1);
        var expiredSubscription = new UserSubscription(
            expiredUser.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL",
            DateTime.UtcNow.AddMonths(-1),
            expiredRenewsAt);
        expiredSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), expiredRenewsAt);
        expiredSubscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        var activeUser = new User("Ativo", "ativo@teste.com", "hash");
        activeUser.SetRole(UserRole.Advanced);
        var activeRenewsAt = DateTime.UtcNow.AddDays(20);
        var activeSubscription = new UserSubscription(
            activeUser.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL",
            DateTime.UtcNow.AddMonths(-1),
            activeRenewsAt);
        activeSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), activeRenewsAt);
        activeSubscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        await dbContext.Users.AddRangeAsync(expiredUser, activeUser);
        await dbContext.UserSubscriptions.AddRangeAsync(expiredSubscription, activeSubscription);
        await dbContext.SaveChangesAsync();

        var sut = new SubscriptionExpirationRobotTask(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext));

        var result = await sut.RunAsync();

        result.ItemsGenerated.Should().Be(1);

        var updatedExpiredSubscription = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == expiredUser.Id);
        updatedExpiredSubscription.Status.Should().Be(UserSubscriptionStatus.Cancelled);
        (await dbContext.Users.SingleAsync(x => x.Id == expiredUser.Id)).Role.Should().Be(UserRole.Basic);

        var updatedActiveSubscription = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == activeUser.Id);
        updatedActiveSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
        (await dbContext.Users.SingleAsync(x => x.Id == activeUser.Id)).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RunAsync_Should_Return_Zero_When_Nothing_Is_Due()
    {
        await using var dbContext = CreateDbContext();

        var result = await new SubscriptionExpirationRobotTask(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext)).RunAsync();

        result.ItemsGenerated.Should().Be(0);
        result.ZeroItemsReasonCode.Should().Be("NO_SUBSCRIPTIONS_DUE");
    }

    [Fact]
    public async Task RunAsync_Should_Expire_PastDue_Subscriptions_After_Grace_Period()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "pastdue@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        // RenewsAt 8 days ago → past 7-day grace period
        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-8));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var result = await new SubscriptionExpirationRobotTask(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext)).RunAsync();

        result.ItemsGenerated.Should().Be(1);

        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.Expired);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Basic);
    }

    [Fact]
    public async Task RunAsync_Should_NOT_Expire_PastDue_Subscriptions_Within_Grace_Period()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDueGrace", "pastduegrace@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        // RenewsAt 3 days ago → still within 7-day grace period
        var renewsAt = DateTime.UtcNow.AddDays(-3);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-3));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var result = await new SubscriptionExpirationRobotTask(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext)).RunAsync();

        result.ItemsGenerated.Should().Be(0);
        result.ZeroItemsReasonCode.Should().Be("NO_SUBSCRIPTIONS_DUE");

        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.PastDue);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Advanced);
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
