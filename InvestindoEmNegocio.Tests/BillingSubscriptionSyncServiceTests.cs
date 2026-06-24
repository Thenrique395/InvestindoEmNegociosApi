using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Stripe;

namespace InvestindoEmNegocio.Tests;

public class BillingSubscriptionSyncServiceTests
{
    private static BillingSubscriptionSyncService CreateSut(
        InvestDbContext dbContext,
        IBillingNotificationService? notificationService = null) =>
        new(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            new BillingCheckoutRepository(dbContext),
            Mock.Of<IPaymentProvider>(),
            notificationService ?? Mock.Of<IBillingNotificationService>(),
            Mock.Of<ILogger<BillingSubscriptionSyncService>>());

    [Fact]
    public async Task SyncAsync_Should_Activate_Subscription_Promote_User_And_Mark_Checkout_As_Paid()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "unpaid", DateTime.UtcNow);

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = CreateSut(dbContext, billingNotificationService.Object);

        var subscription = new ProviderSubscriptionSnapshot("sub_test", "cus_test", "active", null, null, null, null);

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionUpdated, knownCheckout: checkout);

        var storedUser = await dbContext.Users.SingleAsync();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        var storedCheckout = await dbContext.BillingCheckouts.SingleAsync();

        storedUser.Role.Should().Be(UserRole.Advanced);
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
        storedSubscription.ExternalCustomerId.Should().Be("cus_test");
        storedSubscription.ExternalSubscriptionId.Should().Be("sub_test");
        storedCheckout.Status.Should().Be(BillingCheckoutStatus.Paid);
        billingNotificationService.Verify(
            x => x.NotifyApprovedAsync(user.Id, It.Is<BillingCheckout>(c => c.Id == checkout.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_Should_Propagate_DbUpdateConcurrencyException_When_Subscription_Changed_Concurrently()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        await using var dbContext = new InvestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = new User("Teste", "sync-concurrency@teste.com", "hash");
        var localSubscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10));
        localSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10), "cus_test", "sub_concurrency", null);

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(localSubscription);
        await dbContext.SaveChangesAsync();

        // dbContext mantém localSubscription rastreada com a Version pré-corrida. Um segundo
        // DbContext simula um cancelamento do usuário avançando a Version no banco antes do
        // webhook (SyncAsync) conseguir salvar — deve propagar DbUpdateConcurrencyException
        // sem ser engolida, conforme a decisão deliberada de não reter aqui (ver plano).
        await using var dbContextFromCancel = new InvestDbContext(options);
        var subscriptionFromCancel = await dbContextFromCancel.UserSubscriptions.SingleAsync(x => x.Id == localSubscription.Id);
        subscriptionFromCancel.ScheduleCancellation(DateTime.UtcNow);
        await dbContextFromCancel.SaveChangesAsync();

        var sut = CreateSut(dbContext);
        var subscriptionSnapshot = new ProviderSubscriptionSnapshot("sub_concurrency", "cus_test", "active", null, null, null, null);

        await sut.Invoking(x => x.SyncAsync(subscriptionSnapshot, EventTypes.CustomerSubscriptionUpdated))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task SyncAsync_Should_MarkPastDue_And_NotifyFailed_When_Status_Is_PastDue()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "pastdue@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "unpaid", DateTime.UtcNow);

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = CreateSut(dbContext, billingNotificationService.Object);

        var subscription = new ProviderSubscriptionSnapshot("sub_test", "cus_test", "past_due", null, null, null, null);

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionUpdated, knownCheckout: checkout);

        var storedUser = await dbContext.Users.SingleAsync();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        var storedCheckout = await dbContext.BillingCheckouts.SingleAsync();

        storedUser.Role.Should().Be(UserRole.Advanced);
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.PastDue);
        storedCheckout.Status.Should().Be(BillingCheckoutStatus.Failed);
        billingNotificationService.Verify(
            x => x.NotifyFailedAsync(user.Id, It.Is<BillingCheckout>(c => c.Id == checkout.Id), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAsync_Should_Cancel_Subscription_And_Downgrade_User_When_Status_Is_Canceled()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "canceled@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "paid", DateTime.UtcNow);

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        var subscription = new ProviderSubscriptionSnapshot("sub_test", "cus_test", "canceled", null, null, null, null);

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionDeleted, knownCheckout: checkout);

        var storedUser = await dbContext.Users.SingleAsync();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        var storedCheckout = await dbContext.BillingCheckouts.SingleAsync();

        storedUser.Role.Should().Be(UserRole.Basic);
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Cancelled);
        storedCheckout.Status.Should().Be(BillingCheckoutStatus.Cancelled);
    }

    [Fact]
    public async Task SyncAsync_Should_NotifyReactivated_When_Previous_Status_Was_PastDue()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "reactivated@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "paid", DateTime.UtcNow);

        var existingSubscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(-2));
        existingSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(-2), "cus_test", "sub_test", null);
        existingSubscription.MarkPastDue(DateTime.UtcNow.AddDays(-2));

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.UserSubscriptions.AddAsync(existingSubscription);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = CreateSut(dbContext, billingNotificationService.Object);

        var subscription = new ProviderSubscriptionSnapshot("sub_test", "cus_test", "active", null, null, null, null);

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionUpdated, knownCheckout: checkout);

        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
        billingNotificationService.Verify(
            x => x.NotifyReactivatedAsync(user.Id, It.Is<BillingCheckout>(c => c.Id == checkout.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        billingNotificationService.Verify(
            x => x.NotifyApprovedAsync(It.IsAny<Guid>(), It.IsAny<BillingCheckout>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncAsync_Should_NotifyRenewalApproved_When_Previous_Status_Was_Active()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "renewal@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "paid", DateTime.UtcNow);
        checkout.AttachProviderObjects("cus_test", "sub_test", "pi_test", DateTime.UtcNow);
        checkout.MarkPaid("active", "customer.subscription.created", DateTime.UtcNow);

        var existingSubscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10));
        existingSubscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10), "cus_test", "sub_test", null);

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.UserSubscriptions.AddAsync(existingSubscription);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = CreateSut(dbContext, billingNotificationService.Object);

        var subscription = new ProviderSubscriptionSnapshot("sub_test", "cus_test", "active", null, null, null, null);

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionUpdated, knownCheckout: checkout);

        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
        billingNotificationService.Verify(
            x => x.NotifyRenewalApprovedAsync(user.Id, It.Is<BillingCheckout>(c => c.Id == checkout.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        billingNotificationService.Verify(
            x => x.NotifyApprovedAsync(It.IsAny<Guid>(), It.IsAny<BillingCheckout>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DowngradeUserAfterRefundAsync_Should_Refund_Subscription_And_Downgrade_User()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(10), "cus_test", "sub_test", "price_test");

        var checkout = new BillingCheckout(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = CreateSut(dbContext, billingNotificationService.Object);

        await sut.DowngradeUserAfterRefundAsync(checkout);

        var storedUser = await dbContext.Users.SingleAsync();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();

        storedUser.Role.Should().Be(UserRole.Basic);
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Refunded);
        billingNotificationService.Verify(
            x => x.NotifyFailedAsync(
                user.Id,
                It.Is<BillingCheckout>(c => c.Id == checkout.Id),
                It.IsAny<CancellationToken>(),
                "O pagamento foi estornado e o plano voltou para o Essencial."),
            Times.Once);
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
