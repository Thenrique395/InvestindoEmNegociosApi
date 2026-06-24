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
    private static SubscriptionManagementService CreateSut(
        InvestDbContext dbContext,
        IJwtTokenGenerator? jwt = null,
        IMercadoPagoBillingGateway? mercadoPagoBillingGateway = null,
        IBillingCheckoutRepository? billingCheckoutRepository = null)
    {
        var jwtMock = jwt ?? Mock.Of<IJwtTokenGenerator>();
        var userSessionService = new UserSessionService(new RefreshTokenRepository(dbContext), jwtMock, Mock.Of<ILogger<UserSessionService>>());
        return new SubscriptionManagementService(
            new UserRepository(dbContext),
            new UserSubscriptionRepository(dbContext),
            billingCheckoutRepository ?? Mock.Of<IBillingCheckoutRepository>(),
            userSessionService,
            Mock.Of<IStripeBillingGateway>(),
            mercadoPagoBillingGateway ?? Mock.Of<IMercadoPagoBillingGateway>(),
            Options.Create(new StripeOptions()),
            Mock.Of<ILogger<SubscriptionManagementService>>());
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
    public async Task CancelAsync_Should_Retry_And_Succeed_When_Concurrent_Webhook_Updates_Subscription()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        await using var dbContext1 = new InvestDbContext(options);
        await dbContext1.Database.EnsureCreatedAsync();

        var user = new User("Teste", "concurrency@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        var renewsAt = DateTime.UtcNow.AddDays(20);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt);
        await dbContext1.Users.AddAsync(user);
        await dbContext1.UserSubscriptions.AddAsync(subscription);
        await dbContext1.SaveChangesAsync();

        // dbContext1 mantém a entidade rastreada com a Version pré-corrida. Um segundo
        // DbContext simula um webhook concorrente avançando a Version no banco antes do
        // cancelamento de dbContext1 conseguir salvar — deve disparar
        // DbUpdateConcurrencyException e o retry de CancelAsync deve recarregar e reaplicar.
        await using var dbContext2 = new InvestDbContext(options);
        var subscriptionFromWebhook = await dbContext2.UserSubscriptions.SingleAsync(x => x.Id == subscription.Id);
        subscriptionFromWebhook.MarkPastDue(DateTime.UtcNow);
        await dbContext2.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>())).Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var result = await CreateSut(dbContext1, jwt.Object).CancelAsync(user.Id);

        result.Current.AutoRenew.Should().BeFalse();

        await using var verifyContext = new InvestDbContext(options);
        var stored = await verifyContext.UserSubscriptions.SingleAsync(x => x.Id == subscription.Id);
        stored.AutoRenew.Should().BeFalse();
    }

    [Fact]
    public async Task CancelAsync_Should_Cancel_Preapproval_When_Subscription_Is_MercadoPago()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "mp-cancel@teste.com", "hash");
        user.SetRole(UserRole.Advanced);
        await dbContext.Users.AddAsync(user);
        var renewsAt = DateTime.UtcNow.AddDays(20);
        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), renewsAt);
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), renewsAt, null, "preapproval_test", null);
        subscription.SetProvider("mercado_pago");
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>())).Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));
        var mercadoPagoGateway = new Mock<IMercadoPagoBillingGateway>();

        var result = await CreateSut(dbContext, jwt.Object, mercadoPagoGateway.Object).CancelAsync(user.Id);

        result.Current.AutoRenew.Should().BeFalse();
        result.Current.RenewsAt.Should().BeCloseTo(renewsAt, TimeSpan.FromSeconds(1));
        mercadoPagoGateway.Verify(x => x.CancelAsync("preapproval_test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestRefundAsync_Should_Cancel_Preapproval_And_Mark_Refunded_For_MercadoPago()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "mp-refund@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), null, "preapproval_refund", null);
        subscription.SetProvider("mercado_pago");

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>())).Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));
        var mercadoPagoGateway = new Mock<IMercadoPagoBillingGateway>();

        var result = await CreateSut(dbContext, jwt.Object, mercadoPagoGateway.Object).RequestRefundAsync(user.Id);

        result.Current.Status.Should().Be("Refunded");
        (await dbContext.Users.SingleAsync()).Role.Should().Be(UserRole.Basic);
        mercadoPagoGateway.Verify(x => x.CancelAsync("preapproval_refund", It.IsAny<CancellationToken>()), Times.Once);
        mercadoPagoGateway.Verify(x => x.RefundPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestRefundAsync_Should_Refund_Linked_Payment_For_MercadoPago()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "mp-refund-linked@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), null, "preapproval_linked", null);
        subscription.SetProvider("mercado_pago");

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.Generate(It.IsAny<User>())).Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));
        var mercadoPagoGateway = new Mock<IMercadoPagoBillingGateway>();

        var checkout = new BillingCheckout(user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL");
        checkout.AttachProviderObjects(null, "preapproval_linked", "pay-linked-1", DateTime.UtcNow);
        var checkoutRepo = new Mock<IBillingCheckoutRepository>();
        checkoutRepo.Setup(x => x.GetByProviderSubscriptionIdAsync("preapproval_linked", It.IsAny<CancellationToken>())).ReturnsAsync(checkout);

        var result = await CreateSut(dbContext, jwt.Object, mercadoPagoGateway.Object, checkoutRepo.Object).RequestRefundAsync(user.Id);

        result.Current.Status.Should().Be("Refunded");
        mercadoPagoGateway.Verify(x => x.CancelAsync("preapproval_linked", It.IsAny<CancellationToken>()), Times.Once);
        mercadoPagoGateway.Verify(x => x.RefundPaymentAsync("pay-linked-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryPaymentAsync_Should_Throw_NotImplemented_For_MercadoPago_Subscription()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "mp-retry@teste.com", "hash");
        user.SetRole(UserRole.Advanced);

        var subscription = new UserSubscription(
            user.Id, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly,
            59.90m, "BRL", DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(-1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL",
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(-1), null, "preapproval_retry", null);
        subscription.SetProvider("mercado_pago");
        subscription.MarkPastDue(DateTime.UtcNow.AddDays(-1));

        await dbContext.Users.AddAsync(user);
        await dbContext.UserSubscriptions.AddAsync(subscription);
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        await sut.Invoking(x => x.RetryPaymentAsync(user.Id))
            .Should().ThrowAsync<AppProblemException>()
            .WithMessage("*Mercado Pago*");
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
