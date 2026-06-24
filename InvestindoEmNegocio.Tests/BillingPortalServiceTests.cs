using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class BillingPortalServiceTests
{
    private static BillingPortalService CreateSut(
        Mock<IUserSubscriptionRepository> userSubscriptionRepository,
        Mock<IStripeBillingGateway>? stripeGateway = null) =>
        new(
            userSubscriptionRepository.Object,
            (stripeGateway ?? new Mock<IStripeBillingGateway>()).Object,
            Options.Create(new StripeOptions { FrontendBaseUrl = "https://app.test", PortalReturnPath = "/assinatura" }));

    [Fact]
    public async Task CreatePortalSessionAsync_Should_Return_Stripe_Portal_Url_For_Stripe_Subscription()
    {
        var userId = Guid.NewGuid();
        var subscription = new UserSubscription(userId, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), "cus_test", "sub_test", null);

        var repo = new Mock<IUserSubscriptionRepository>();
        repo.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var stripeGateway = new Mock<IStripeBillingGateway>();
        stripeGateway.Setup(x => x.CreatePortalSessionAsync("cus_test", It.IsAny<CancellationToken>())).ReturnsAsync("https://stripe.test/portal");

        var sut = CreateSut(repo, stripeGateway);

        var result = await sut.CreatePortalSessionAsync(userId);

        result.Url.Should().Be("https://stripe.test/portal");
    }

    [Fact]
    public async Task CreatePortalSessionAsync_Should_Redirect_To_Own_Subscription_Page_For_MercadoPago_Subscription()
    {
        var userId = Guid.NewGuid();
        var subscription = new UserSubscription(userId, "advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Activate("advanced", UserRole.Advanced, SubscriptionBillingCycle.Monthly, 59.90m, "BRL", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), null, "preapproval_test", null);
        subscription.SetProvider("mercado_pago");

        var repo = new Mock<IUserSubscriptionRepository>();
        repo.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var stripeGateway = new Mock<IStripeBillingGateway>();
        var sut = CreateSut(repo, stripeGateway);

        var result = await sut.CreatePortalSessionAsync(userId);

        result.Url.Should().Be("https://app.test/assinatura");
        stripeGateway.Verify(x => x.CreatePortalSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePortalSessionAsync_Should_Throw_When_Subscription_Not_Found()
    {
        var userId = Guid.NewGuid();
        var repo = new Mock<IUserSubscriptionRepository>();
        repo.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((UserSubscription?)null);

        var sut = CreateSut(repo);

        await sut.Invoking(x => x.CreatePortalSessionAsync(userId))
            .Should().ThrowAsync<AppProblemException>();
    }
}
