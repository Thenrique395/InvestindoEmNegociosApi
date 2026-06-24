using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using Moq;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Tests;

public class SubscriptionExpirationRobotTaskTests
{
    private static SubscriptionExpirationRobotTask CreateSut(
        InvestDbContext dbContext,
        IBillingNotificationService? notificationService = null,
        IPaymentProvider? paymentProvider = null,
        IBillingSubscriptionSyncService? billingSubscriptionSyncService = null)
    {
        var resolverMock = new Mock<IPaymentProviderResolver>();
        resolverMock.Setup(x => x.Resolve(It.IsAny<string>())).Returns(paymentProvider ?? Mock.Of<IPaymentProvider>());

        return new(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            notificationService ?? Mock.Of<IBillingNotificationService>(),
            resolverMock.Object,
            billingSubscriptionSyncService ?? Mock.Of<IBillingSubscriptionSyncService>(),
            Mock.Of<ILogger<SubscriptionExpirationRobotTask>>());
    }

    [Fact]
    public async Task RunAsync_Should_Downgrade_Subscriptions_Past_RenewsAt_With_AutoRenew_Disabled()
    {
        await using var dbContext = CreateDbContext();

        var expiredUser = new User("Expirado", "expirado@teste.com", "hash");
        expiredUser.SetRole(UserRole.Advanced);
        var expiredRenewsAt = DateTime.UtcNow.AddDays(-1);
        var expiredSubscription = new UserSubscription(
            expiredUser.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), expiredRenewsAt);
        expiredSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), expiredRenewsAt);
        expiredSubscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        var activeUser = new User("Ativo", "ativo@teste.com", "hash");
        activeUser.SetRole(UserRole.Advanced);
        var activeRenewsAt = DateTime.UtcNow.AddDays(20);
        var activeSubscription = new UserSubscription(
            activeUser.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), activeRenewsAt);
        activeSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), activeRenewsAt);
        activeSubscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        await dbContext.Users.AddRangeAsync(expiredUser, activeUser);
        await dbContext.UserSubscriptions.AddRangeAsync(expiredSubscription, activeSubscription);
        await dbContext.SaveChangesAsync();

        var result = await CreateSut(dbContext).RunAsync();

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

        var result = await CreateSut(dbContext).RunAsync();

        result.ItemsGenerated.Should().Be(0);
        result.ZeroItemsReasonCode.Should().Be("NO_SUBSCRIPTIONS_DUE");
    }

    [Fact]
    public async Task RunAsync_Should_Expire_PastDue_Subscriptions_After_Grace_Period()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "pastdue@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-8));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var result = await CreateSut(dbContext).RunAsync();

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

        var renewsAt = DateTime.UtcNow.AddDays(-3);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-3));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var result = await CreateSut(dbContext).RunAsync();

        result.ItemsGenerated.Should().Be(0);
        result.ZeroItemsReasonCode.Should().Be("NO_SUBSCRIPTIONS_DUE");

        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.PastDue);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RunAsync_Should_Notify_Downgraded_For_PastDue_After_Grace_Period()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "notify@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(renewsAt);

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();

        await CreateSut(dbContext, billingNotificationService.Object).RunAsync();

        billingNotificationService.Verify(
            x => x.NotifyDowngradedAsync(user.Id, "advanced", It.IsAny<CancellationToken>()),
            Times.Once);
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Basic);
    }

    [Fact]
    public async Task RunAsync_Should_Send_Grace_Period_Reminder_For_Approaching_Expiry()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("ApproachingGrace", "reminder@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        // RenewsAt 6.5 days ago → in last 24h of grace period (window: -7d to -6d)
        var renewsAt = DateTime.UtcNow.AddDays(-6.5);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.MarkPastDue(renewsAt);

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();

        await CreateSut(dbContext, billingNotificationService.Object).RunAsync();

        billingNotificationService.Verify(
            x => x.NotifyGracePeriodReminderAsync(user.Id, "advanced", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // user not downgraded — still in grace period
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RunAsync_Should_Skip_Admin_Users()
    {
        await using var dbContext = CreateDbContext();

        var adminUser = new User("Admin", "admin@teste.com", "hash");
        adminUser.SetRole(UserRole.Admin);

        var renewsAt = DateTime.UtcNow.AddDays(-1);
        var subscription = new UserSubscription(
            adminUser.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        await dbContext.Users.AddAsync(adminUser);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var result = await CreateSut(dbContext).RunAsync();

        result.ItemsGenerated.Should().Be(0);
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Admin);
        (await dbContext.UserSubscriptions.SingleAsync()).Status.Should().Be(UserSubscriptionStatus.Active);
    }

    [Fact]
    public async Task RunAsync_Should_Sync_Instead_Of_Downgrade_When_Stripe_Shows_Active()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "pastdue-active@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt, "cus_test", "sub_test_active", null);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-8));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var snapshot = new ProviderSubscriptionSnapshot("sub_test_active", "cus_test", "active", null, null, null, null);
        var providerMock = new Mock<IPaymentProvider>();
        providerMock
            .Setup(x => x.GetSubscriptionAsync("sub_test_active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var syncMock = new Mock<IBillingSubscriptionSyncService>();

        var result = await CreateSut(dbContext, paymentProvider: providerMock.Object, billingSubscriptionSyncService: syncMock.Object).RunAsync();

        result.ItemsGenerated.Should().Be(1);
        syncMock.Verify(x => x.SyncAsync(snapshot, "robot.reconcile", It.IsAny<CancellationToken>(), null), Times.Once);

        // não deve ter feito downgrade local
        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.PastDue); // sync service não foi real — estado local inalterado
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RunAsync_Should_Sync_Instead_Of_Cancel_When_AutoRenew_Disabled_But_Stripe_Shows_Active()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("Resumido", "resumido@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-1);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt, "cus_test", "sub_test_resumed", null);
        // usuário cancelou a renovação no app, mas depois retomou direto no Stripe — webhook de retomada perdido
        subscription.ScheduleCancellation(DateTime.UtcNow.AddDays(-5));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var snapshot = new ProviderSubscriptionSnapshot("sub_test_resumed", "cus_test", "active", null, null, null, null);
        var providerMock = new Mock<IPaymentProvider>();
        providerMock
            .Setup(x => x.GetSubscriptionAsync("sub_test_resumed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var syncMock = new Mock<IBillingSubscriptionSyncService>();

        var result = await CreateSut(dbContext, paymentProvider: providerMock.Object, billingSubscriptionSyncService: syncMock.Object).RunAsync();

        result.ItemsGenerated.Should().Be(1);
        syncMock.Verify(x => x.SyncAsync(snapshot, "robot.reconcile", It.IsAny<CancellationToken>(), null), Times.Once);

        // não deve ter cancelado localmente
        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.Active);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Advanced);
    }

    [Fact]
    public async Task RunAsync_Should_Downgrade_When_Stripe_Still_Shows_PastDue()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "pastdue-stripe@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt, "cus_test", "sub_test_past_due", null);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-8));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var providerMock = new Mock<IPaymentProvider>();
        providerMock
            .Setup(x => x.GetSubscriptionAsync("sub_test_past_due", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSubscriptionSnapshot("sub_test_past_due", "cus_test", "past_due", null, null, null, null));

        var syncMock = new Mock<IBillingSubscriptionSyncService>();

        var result = await CreateSut(dbContext, paymentProvider: providerMock.Object, billingSubscriptionSyncService: syncMock.Object).RunAsync();

        result.ItemsGenerated.Should().Be(1);
        syncMock.Verify(x => x.SyncAsync(It.IsAny<ProviderSubscriptionSnapshot>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), null), Times.Never);

        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.Expired);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Basic);
    }

    [Fact]
    public async Task RunAsync_Should_Downgrade_With_Graceful_Degradation_When_Stripe_Check_Fails()
    {
        await using var dbContext = CreateDbContext();

        var user = new User("PastDue", "pastdue-stripe-error@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var renewsAt = DateTime.UtcNow.AddDays(-8);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt, "cus_test", "sub_test_error", null);
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-8));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var providerMock = new Mock<IPaymentProvider>();
        providerMock
            .Setup(x => x.GetSubscriptionAsync("sub_test_error", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe unavailable"));

        var result = await CreateSut(dbContext, paymentProvider: providerMock.Object).RunAsync();

        // Degradação graciosa: downgrade prossegue mesmo com Stripe indisponível
        result.ItemsGenerated.Should().Be(1);
        var updated = await dbContext.UserSubscriptions.SingleAsync(x => x.UserId == user.Id);
        updated.Status.Should().Be(UserSubscriptionStatus.Expired);
        (await dbContext.Users.SingleAsync(x => x.Id == user.Id)).Role.Should().Be(UserRole.Basic);
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
