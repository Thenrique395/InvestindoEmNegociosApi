using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;
using Stripe;
using Stripe.Checkout;

namespace InvestindoEmNegocio.Tests;

public class StripeBillingWebhookProcessorTests
{
    [Fact]
    public async Task ProcessAsync_Should_Apply_Session_Status_And_Sync_Subscription_For_Checkout_Completion()
    {
        var checkout = new BillingCheckout(
            Guid.NewGuid(),
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL");
        var session = new Session
        {
            Id = "cs_test",
            SubscriptionId = "sub_test",
            PaymentStatus = "paid"
        };
        var stripeEvent = new Event
        {
            Id = "evt_test",
            Type = EventTypes.CheckoutSessionCompleted,
            Data = new EventData
            {
                Object = session
            }
        };
        var webhookLog = new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type, "{}");

        var billingCheckoutRepository = new Mock<IBillingCheckoutRepository>();
        var billingSubscriptionSyncService = new Mock<IBillingSubscriptionSyncService>();
        billingSubscriptionSyncService
            .Setup(x => x.ResolveCheckoutFromSessionAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkout);

        var sut = new StripeBillingWebhookProcessor(
            billingCheckoutRepository.Object,
            billingSubscriptionSyncService.Object);

        await sut.ProcessAsync(stripeEvent, webhookLog);

        webhookLog.UserId.Should().Be(checkout.UserId);
        webhookLog.BillingCheckoutId.Should().Be(checkout.Id);
        billingSubscriptionSyncService.Verify(
            x => x.ApplySessionStatus(checkout, session, It.IsAny<DateTime>(), EventTypes.CheckoutSessionCompleted),
            Times.Once);
        billingCheckoutRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        billingSubscriptionSyncService.Verify(
            x => x.SyncByExternalSubscriptionAsync("sub_test", checkout, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Mark_Refund_And_Downgrade_User_For_Charge_Refunded()
    {
        var checkout = new BillingCheckout(
            Guid.NewGuid(),
            "advanced",
            UserRole.Advanced,
            SubscriptionBillingCycle.Monthly,
            59.90m,
            "BRL");
        var charge = new Charge
        {
            PaymentIntentId = "pi_test"
        };
        var stripeEvent = new Event
        {
            Id = "evt_refund",
            Type = EventTypes.ChargeRefunded,
            Data = new EventData
            {
                Object = charge
            }
        };
        var webhookLog = new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type, "{}");

        var billingCheckoutRepository = new Mock<IBillingCheckoutRepository>();
        var billingSubscriptionSyncService = new Mock<IBillingSubscriptionSyncService>();
        billingSubscriptionSyncService
            .Setup(x => x.FindCheckoutByPaymentIntentAsync("pi_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkout);

        var sut = new StripeBillingWebhookProcessor(
            billingCheckoutRepository.Object,
            billingSubscriptionSyncService.Object);

        await sut.ProcessAsync(stripeEvent, webhookLog);

        checkout.Status.Should().Be(BillingCheckoutStatus.Refunded);
        webhookLog.UserId.Should().Be(checkout.UserId);
        webhookLog.BillingCheckoutId.Should().Be(checkout.Id);
        billingCheckoutRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        billingSubscriptionSyncService.Verify(
            x => x.DowngradeUserAfterRefundAsync(checkout, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
