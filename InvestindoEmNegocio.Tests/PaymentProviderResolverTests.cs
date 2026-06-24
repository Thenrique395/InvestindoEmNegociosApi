using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PaymentProviderResolverTests
{
    [Theory]
    [InlineData("mercado_pago")]
    [InlineData("MERCADO_PAGO")]
    [InlineData(" mercado_pago ")]
    public void Resolve_Should_Return_MercadoPago_Gateway(string provider)
    {
        // Mock.As<T>() adiciona IPaymentProvider ao MESMO proxy, simulando o cast explícito
        // real que MercadoPagoBillingGateway faz em produção (uma única classe concreta
        // implementando os dois contratos).
        var stripeGatewayMock = new Mock<IStripeBillingGateway>();
        stripeGatewayMock.As<IPaymentProvider>();
        var mercadoPagoGatewayMock = new Mock<IMercadoPagoBillingGateway>();
        mercadoPagoGatewayMock.As<IPaymentProvider>();

        var sut = new PaymentProviderResolver(stripeGatewayMock.Object, mercadoPagoGatewayMock.Object);

        var result = sut.Resolve(provider);

        result.Should().BeSameAs(mercadoPagoGatewayMock.As<IPaymentProvider>().Object);
    }

    [Theory]
    [InlineData("stripe")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-provider")]
    public void Resolve_Should_Default_To_Stripe_Gateway(string? provider)
    {
        var stripeGatewayMock = new Mock<IStripeBillingGateway>();
        stripeGatewayMock.As<IPaymentProvider>();
        var mercadoPagoGatewayMock = new Mock<IMercadoPagoBillingGateway>();
        mercadoPagoGatewayMock.As<IPaymentProvider>();

        var sut = new PaymentProviderResolver(stripeGatewayMock.Object, mercadoPagoGatewayMock.Object);

        var result = sut.Resolve(provider);

        result.Should().BeSameAs(stripeGatewayMock.As<IPaymentProvider>().Object);
    }
}
