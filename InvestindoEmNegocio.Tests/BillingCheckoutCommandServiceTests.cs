using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Tests;

public class BillingCheckoutCommandServiceTests
{
    private static (BillingCheckoutCommandService Sut, Mock<IStripeBillingGateway> StripeGateway, Mock<IMercadoPagoBillingGateway> MercadoPagoGateway, Mock<IBillingCheckoutRepository> CheckoutRepo)
        CreateSut(User user, string primaryProvider)
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var userSubscriptionRepository = new Mock<IUserSubscriptionRepository>();
        userSubscriptionRepository.Setup(x => x.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserSubscription?)null);

        var checkoutRepository = new Mock<IBillingCheckoutRepository>();

        var stripeGateway = new Mock<IStripeBillingGateway>();
        stripeGateway
            .Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<User>(), It.IsAny<BillingCheckout>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { Id = "cs_test", Url = "https://stripe.test/checkout", PaymentStatus = "unpaid", CustomerId = "cus_test", SubscriptionId = "sub_test" });

        var mercadoPagoGateway = new Mock<IMercadoPagoBillingGateway>();
        mercadoPagoGateway
            .Setup(x => x.CreatePreapprovalAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Domain.Enums.SubscriptionBillingCycle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPreapproval("preapproval_test", "pending", "checkout-ref", user.Email, "https://mercadopago.test/checkout", 29.90m, "BRL", null));

        var notificationService = Mock.Of<IBillingNotificationService>();

        var sut = new BillingCheckoutCommandService(
            userRepository.Object,
            userSubscriptionRepository.Object,
            checkoutRepository.Object,
            stripeGateway.Object,
            mercadoPagoGateway.Object,
            Options.Create(new BillingOptions { PrimaryProvider = primaryProvider }),
            notificationService,
            Mock.Of<ILogger<BillingCheckoutCommandService>>());

        return (sut, stripeGateway, mercadoPagoGateway, checkoutRepository);
    }

    [Fact]
    public async Task StartCheckoutAsync_Should_Use_Stripe_By_Default()
    {
        var user = new User("Teste", "user@teste.com", "hash");
        var (sut, stripeGateway, mercadoPagoGateway, _) = CreateSut(user, "stripe");

        var response = await sut.StartCheckoutAsync(user.Id, new StartBillingCheckoutRequest("intermediate", "Monthly"));

        response.Provider.Should().Be("stripe");
        response.CheckoutUrl.Should().Be("https://stripe.test/checkout");
        stripeGateway.Verify(x => x.CreateCheckoutSessionAsync(It.IsAny<User>(), It.IsAny<BillingCheckout>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        mercadoPagoGateway.Verify(x => x.CreatePreapprovalAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Domain.Enums.SubscriptionBillingCycle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartCheckoutAsync_Should_Use_MercadoPago_When_Configured_As_Primary()
    {
        var user = new User("Teste", "user@teste.com", "hash");
        var (sut, stripeGateway, mercadoPagoGateway, _) = CreateSut(user, "mercado_pago");

        var response = await sut.StartCheckoutAsync(user.Id, new StartBillingCheckoutRequest("intermediate", "Monthly"));

        response.Provider.Should().Be("mercado_pago");
        response.CheckoutUrl.Should().Be("https://mercadopago.test/checkout");
        mercadoPagoGateway.Verify(
            x => x.CreatePreapprovalAsync(It.IsAny<string>(), user.Email, It.IsAny<string>(), It.IsAny<decimal>(), "BRL", Domain.Enums.SubscriptionBillingCycle.Monthly, It.IsAny<CancellationToken>()),
            Times.Once);
        stripeGateway.Verify(x => x.CreateCheckoutSessionAsync(It.IsAny<User>(), It.IsAny<BillingCheckout>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
