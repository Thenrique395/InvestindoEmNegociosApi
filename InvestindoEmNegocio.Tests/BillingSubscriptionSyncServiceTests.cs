using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stripe;

namespace InvestindoEmNegocio.Tests;

public class BillingSubscriptionSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_Should_Activate_Subscription_Promote_User_And_Mark_Checkout_As_Paid()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        var checkout = new BillingCheckout(
            user.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL");
        checkout.Start("cs_test", "https://checkout.test", DateTime.UtcNow.AddMinutes(30), "unpaid", DateTime.UtcNow);

        await dbContext.Users.AddAsync(user);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = new BillingSubscriptionSyncService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            new BillingCheckoutRepository(dbContext),
            Mock.Of<IStripeBillingGateway>(),
            billingNotificationService.Object);

        var subscription = new Subscription
        {
            Id = "sub_test",
            CustomerId = "cus_test",
            Status = "active"
        };

        await sut.SyncAsync(subscription, EventTypes.CustomerSubscriptionUpdated, knownCheckout: checkout);

        var storedUser = await dbContext.Users.SingleAsync();
        var storedSubscription = await dbContext.UserSubscriptions.SingleAsync();
        var storedCheckout = await dbContext.BillingCheckouts.SingleAsync();

        storedUser.Role.Should().Be(UserRole.Advanced);
        storedSubscription.Status.Should().Be(UserSubscriptionStatus.Active);
        storedSubscription.ExternalCustomerId.Should().Be("cus_test");
        storedSubscription.ExternalSubscriptionId.Should().Be("sub_test");
        storedCheckout.Status.Should().Be(BillingCheckoutStatus.Paid);
        storedCheckout.ProviderSubscriptionId.Should().Be("sub_test");
        billingNotificationService.Verify(
            x => x.NotifyApprovedAsync(user.Id, It.Is<BillingCheckout>(c => c.Id == checkout.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DowngradeUserAfterRefundAsync_Should_Refund_Subscription_And_Downgrade_User()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "teste@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        var subscription = new UserSubscription(
            user.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL",
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow.AddDays(10));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddDays(10), "cus_test", "sub_test", "price_test");

        var checkout = new BillingCheckout(
            user.Id,
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL");

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.BillingCheckouts.AddAsync(checkout);
        await dbContext.SaveChangesAsync();

        var billingNotificationService = new Mock<IBillingNotificationService>();
        var sut = new BillingSubscriptionSyncService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            new BillingCheckoutRepository(dbContext),
            Mock.Of<IStripeBillingGateway>(),
            billingNotificationService.Object);

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
