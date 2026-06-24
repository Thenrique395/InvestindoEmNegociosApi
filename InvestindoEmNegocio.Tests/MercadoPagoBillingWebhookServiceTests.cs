using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class MercadoPagoBillingWebhookServiceTests
{
    private static MercadoPagoBillingWebhookService CreateSut(
        IBillingWebhookEventRepository repo,
        IMercadoPagoBillingGateway gateway,
        IBillingSubscriptionSyncService syncService,
        IBillingCheckoutRepository? checkoutRepo = null) =>
        new(repo, checkoutRepo ?? Mock.Of<IBillingCheckoutRepository>(), gateway, syncService, Mock.Of<ILogger<MercadoPagoBillingWebhookService>>());

    private const string ValidPayload = """
        {"id":111,"type":"subscription_preapproval","action":"updated","date_created":"2026-06-23T10:00:00Z","data":{"id":"preapproval-1"}}
        """;

    [Fact]
    public async Task ProcessWebhookAsync_Should_Throw_When_Signature_Invalid()
    {
        var repo = new Mock<IBillingWebhookEventRepository>();
        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("preapproval-1", "req-1", "bad-signature")).Returns(false);
        var syncService = new Mock<IBillingSubscriptionSyncService>();

        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object);

        Func<Task> act = async () => await sut.ProcessWebhookAsync(ValidPayload, "req-1", "bad-signature");

        await act.Should().ThrowAsync<InvestindoEmNegocio.Application.Exceptions.AppProblemException>();
        repo.Verify(x => x.AddAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Skip_When_Already_Processed()
    {
        var repo = new Mock<IBillingWebhookEventRepository>();
        repo.Setup(x => x.GetByProviderEventIdAsync("mercado_pago", "111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent("111", "subscription_preapproval", "{}", provider: "mercado_pago"));
        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("preapproval-1", "req-1", "sig")).Returns(true);
        var syncService = new Mock<IBillingSubscriptionSyncService>();

        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object);

        await sut.ProcessWebhookAsync(ValidPayload, "req-1", "sig");

        gateway.Verify(x => x.GetPreapprovalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        syncService.Verify(x => x.SyncAsync(It.IsAny<ProviderSubscriptionSnapshot>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), null), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Fetch_Preapproval_And_Sync_For_Preapproval_Type()
    {
        var repo = new Mock<IBillingWebhookEventRepository>();
        repo.Setup(x => x.GetByProviderEventIdAsync("mercado_pago", "111", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        var preapproval = new MercadoPagoPreapproval("preapproval-1", "authorized", "checkout-guid", "user@example.com", null, 29.90m, "BRL", null);
        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("preapproval-1", "req-1", "sig")).Returns(true);
        gateway.Setup(x => x.GetPreapprovalAsync("preapproval-1", It.IsAny<CancellationToken>())).ReturnsAsync(preapproval);

        var syncService = new Mock<IBillingSubscriptionSyncService>();

        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object);

        await sut.ProcessWebhookAsync(ValidPayload, "req-1", "sig");

        repo.Verify(x => x.AddAsync(It.Is<BillingWebhookEvent>(e => e.Provider == "mercado_pago" && e.ProviderEventId == "111"), It.IsAny<CancellationToken>()), Times.Once);
        syncService.Verify(
            x => x.SyncAsync(
                It.Is<ProviderSubscriptionSnapshot>(s => s.ExternalId == "preapproval-1" && s.Status == "active"),
                "mercadopago.subscription_preapproval.updated",
                It.IsAny<CancellationToken>(),
                null),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Ignore_Unknown_Notification_Types()
    {
        const string payload = """
            {"id":333,"type":"merchant_order","action":"created","data":{"id":"order-1"}}
            """;
        var repo = new Mock<IBillingWebhookEventRepository>();
        repo.Setup(x => x.GetByProviderEventIdAsync("mercado_pago", "333", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("order-1", "req-1", "sig")).Returns(true);
        var syncService = new Mock<IBillingSubscriptionSyncService>();

        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object);

        await sut.ProcessWebhookAsync(payload, "req-1", "sig");

        gateway.Verify(x => x.GetPreapprovalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gateway.Verify(x => x.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        syncService.Verify(x => x.SyncAsync(It.IsAny<ProviderSubscriptionSnapshot>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), null), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Link_Payment_To_Checkout_For_Payment_Type()
    {
        const string payload = """
            {"id":222,"type":"payment","action":"created","data":{"id":"pay-1"}}
            """;
        var checkoutId = Guid.NewGuid();
        var repo = new Mock<IBillingWebhookEventRepository>();
        repo.Setup(x => x.GetByProviderEventIdAsync("mercado_pago", "222", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("pay-1", "req-1", "sig")).Returns(true);
        gateway.Setup(x => x.GetPaymentAsync("pay-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment("pay-1", "approved", checkoutId.ToString(), 29.90m));

        var checkout = new BillingCheckout(
            Guid.NewGuid(), "advanced", InvestindoEmNegocio.Domain.Enums.UserRole.Advanced,
            InvestindoEmNegocio.Domain.Enums.SubscriptionBillingCycle.Monthly, 29.90m, "BRL");
        var checkoutRepo = new Mock<IBillingCheckoutRepository>();
        checkoutRepo.Setup(x => x.GetByIdAsync(checkoutId, It.IsAny<CancellationToken>())).ReturnsAsync(checkout);

        var syncService = new Mock<IBillingSubscriptionSyncService>();
        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object, checkoutRepo.Object);

        await sut.ProcessWebhookAsync(payload, "req-1", "sig");

        checkout.ProviderPaymentIntentId.Should().Be("pay-1");
        checkoutRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Skip_Payment_Link_When_External_Reference_Is_Not_A_Guid()
    {
        const string payload = """
            {"id":444,"type":"payment","action":"created","data":{"id":"pay-2"}}
            """;
        var repo = new Mock<IBillingWebhookEventRepository>();
        repo.Setup(x => x.GetByProviderEventIdAsync("mercado_pago", "444", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        var gateway = new Mock<IMercadoPagoBillingGateway>();
        gateway.Setup(x => x.ValidateWebhookSignature("pay-2", "req-1", "sig")).Returns(true);
        gateway.Setup(x => x.GetPaymentAsync("pay-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment("pay-2", "approved", "not-a-guid", 29.90m));

        var checkoutRepo = new Mock<IBillingCheckoutRepository>();
        var syncService = new Mock<IBillingSubscriptionSyncService>();
        var sut = CreateSut(repo.Object, gateway.Object, syncService.Object, checkoutRepo.Object);

        await sut.ProcessWebhookAsync(payload, "req-1", "sig");

        checkoutRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        checkoutRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
