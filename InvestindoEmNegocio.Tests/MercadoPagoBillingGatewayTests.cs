using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class MercadoPagoBillingGatewayTests
{
    private const string WebhookSecret = "test-webhook-secret";
    private const string AccessToken = "TEST-access-token";

    [Fact]
    public void ValidateWebhookSignature_Should_Return_True_For_Valid_Signature()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        const string dataId = "123456";
        const string requestId = "req-1";
        const string ts = "1700000000";
        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var validHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        var signatureHeader = $"ts={ts},v1={validHash}";

        sut.ValidateWebhookSignature(dataId, requestId, signatureHeader).Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhookSignature_Should_Return_False_For_Tampered_Hash()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var signatureHeader = "ts=1700000000,v1=0000000000000000000000000000000000000000000000000000000000000000";

        sut.ValidateWebhookSignature("123456", "req-1", signatureHeader).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_Should_Return_False_When_Header_Missing()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        sut.ValidateWebhookSignature("123456", "req-1", null).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_Should_Return_False_When_Webhook_Secret_Not_Configured()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))), webhookSecret: "");

        sut.ValidateWebhookSignature("123456", "req-1", "ts=1,v1=abc").Should().BeFalse();
    }

    [Fact]
    public async Task GetPreapprovalAsync_Should_Map_Response_Fields()
    {
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.Should().Be("/preapproval/preapproval-id-1");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = "preapproval-id-1",
                    status = "authorized",
                    external_reference = "checkout-guid-1",
                    payer_email = "user@example.com",
                    init_point = "https://mp.example/checkout",
                    next_payment_date = "2026-08-01T00:00:00Z",
                    auto_recurring = new { transaction_amount = 29.90m, currency_id = "BRL" }
                })
            });
        });
        var sut = CreateSut(handler);

        var result = await sut.GetPreapprovalAsync("preapproval-id-1");

        result.Id.Should().Be("preapproval-id-1");
        result.Status.Should().Be("authorized");
        result.ExternalReference.Should().Be("checkout-guid-1");
        result.PayerEmail.Should().Be("user@example.com");
        result.TransactionAmount.Should().Be(29.90m);
        result.CurrencyId.Should().Be("BRL");
    }

    [Fact]
    public async Task GetSubscriptionAsync_Via_IPaymentProvider_Should_Return_Canonical_Snapshot()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "preapproval-id-2",
                status = "authorized",
                external_reference = "checkout-guid-2",
                payer_email = "user2@example.com",
                auto_recurring = new { transaction_amount = 59.90m, currency_id = "BRL" }
            })
        }));
        IPaymentProvider sut = CreateSut(handler);

        var snapshot = await sut.GetSubscriptionAsync("preapproval-id-2");

        snapshot.ExternalId.Should().Be("preapproval-id-2");
        snapshot.Status.Should().Be("active"); // "authorized" -> canônico "active"
        snapshot.Metadata.Should().ContainKey("checkoutId").WhoseValue.Should().Be("checkout-guid-2");
    }

    [Fact]
    public async Task CreatePreapprovalAsync_Should_Send_Expected_AutoRecurring_For_Yearly_Cycle()
    {
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "new-preapproval", status = "pending" })
            };
        });
        var sut = CreateSut(handler);

        await sut.CreatePreapprovalAsync(
            "Plano Advanced - Anual",
            "user@example.com",
            "checkout-guid-3",
            599m,
            "BRL",
            SubscriptionBillingCycle.Yearly);

        capturedBody.Should().NotBeNull();
        capturedBody!.Should().Contain("\"frequency\":12");
        capturedBody.Should().Contain("\"frequency_type\":\"months\"");
        capturedBody.Should().Contain("\"transaction_amount\":599");
    }

    [Fact]
    public async Task CreatePreapprovalAsync_Should_Throw_When_AccessToken_Not_Configured()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))), accessToken: "");

        Func<Task> act = async () => await sut.CreatePreapprovalAsync("r", "e@x.com", "ref", 10m, "BRL", SubscriptionBillingCycle.Monthly);

        await act.Should().ThrowAsync<InvestindoEmNegocio.Application.Exceptions.AppProblemException>();
    }

    [Fact]
    public async Task GetPaymentAsync_Should_Map_Payment_Response()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = 123456789L,
                status = "approved",
                external_reference = "checkout-guid-4",
                transaction_amount = 59.90m
            })
        }));
        var sut = CreateSut(handler);

        var payment = await sut.GetPaymentAsync("123456789");

        payment.Id.Should().Be("123456789");
        payment.Status.Should().Be("approved");
        payment.ExternalReference.Should().Be("checkout-guid-4");
        payment.TransactionAmount.Should().Be(59.90m);
    }

    [Fact]
    public async Task RefundPaymentAsync_Should_Post_To_Refunds_Endpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var sut = CreateSut(handler);

        await sut.RefundPaymentAsync("123456789");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/v1/payments/123456789/refunds");
    }

    [Fact]
    public async Task RefundPaymentAsync_Should_Throw_When_AccessToken_Not_Configured()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))), accessToken: "");

        Func<Task> act = async () => await sut.RefundPaymentAsync("123456789");

        await act.Should().ThrowAsync<InvestindoEmNegocio.Application.Exceptions.AppProblemException>();
    }

    private static MercadoPagoBillingGateway CreateSut(
        HttpMessageHandler handler,
        string accessToken = AccessToken,
        string webhookSecret = WebhookSecret)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com") };
        var options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = accessToken,
            WebhookSecret = webhookSecret
        });
        return new MercadoPagoBillingGateway(httpClient, options);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
