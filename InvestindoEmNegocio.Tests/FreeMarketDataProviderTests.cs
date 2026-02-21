using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace InvestindoEmNegocio.Tests;

public class FreeMarketDataProviderTests
{
    [Fact]
    public async Task GetQuoteAsync_Should_Return_Parsed_Data_When_Brapi_Succeeds()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "results": [
                {
                  "symbol": "PETR4",
                  "regularMarketPrice": 31.25,
                  "regularMarketChangePercent": 1.2,
                  "currency": "BRL",
                  "longName": "Petrobras",
                  "logourl": "https://logo",
                  "regularMarketTime": "2026-02-21T12:00:00Z"
                }
              ]
            }
            """)
        }));

        var sut = CreateSut(handler, "token-123");

        var result = await sut.GetQuoteAsync("petr4", CancellationToken.None);

        result.Symbol.Should().Be("PETR4");
        result.Price.Should().Be(31.25m);
        result.ChangePercent.Should().Be(1.2m);
        result.Source.Should().Contain("BRAPI");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("token=token-123");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task GetQuoteAsync_Should_Return_Fallback_When_Brapi_Fails()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var sut = CreateSut(handler);

        var result = await sut.GetQuoteAsync("PETR4", CancellationToken.None);

        result.Symbol.Should().Be("PETR4");
        result.Price.Should().BeNull();
        result.Source.Should().Contain("Sem resposta da BRAPI");
    }

    [Fact]
    public async Task GetHistoryAsync_Should_Parse_Historical_Points()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($@"
            {{
              ""results"": [
                {{
                  ""symbol"": ""VALE3"",
                  ""regularMarketPrice"": 55.4,
                  ""regularMarketChangePercent"": -0.5,
                  ""currency"": ""BRL"",
                  ""shortName"": ""Vale"",
                  ""regularMarketTime"": ""2026-02-21T12:00:00Z"",
                  ""historicalDataPrice"": [
                    {{ ""date"": {now}, ""close"": 54.2 }},
                    {{ ""date"": {now - 86400}, ""close"": 53.8 }}
                  ]
                }}
              ]
            }}")
        }));

        var sut = CreateSut(handler);

        var result = await sut.GetHistoryAsync("vale3", "1y", CancellationToken.None);

        result.Symbol.Should().Be("VALE3");
        result.Period.Should().Be("1y");
        result.Points.Should().HaveCount(2);
        result.Points[0].Close.Should().Be(54.2m);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("range=1y");
    }

    [Fact]
    public async Task GetSnapshotsAsync_Should_Normalize_And_Deduplicate_Symbols()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "results": [
                { "symbol": "PETR4", "regularMarketPrice": 30.1, "regularMarketChangePercent": 0.8, "currency": "BRL" },
                { "symbol": "VALE3", "regularMarketPrice": 54.7, "regularMarketChangePercent": -0.3, "currency": "BRL" }
              ]
            }
            """)
        }));

        var sut = CreateSut(handler);

        var result = await sut.GetSnapshotsAsync(["petr4", "PETR4", " vale3 ", ""], CancellationToken.None);

        result.Should().HaveCount(2);
        result.Keys.Should().Contain(["PETR4", "VALE3"]);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("PETR4%2CVALE3");
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_Fallback_When_Request_Fails()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var sut = CreateSut(handler);

        var result = await sut.GetProfileAsync("petr4", CancellationToken.None);

        result.Symbol.Should().Be("PETR4");
        result.Source.Should().Contain("Sem resposta da BRAPI");
        result.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_Data_When_Brapi_Succeeds()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "results": [
                {
                  "symbol": "PETR4",
                  "shortName": "Petro",
                  "logourl": "https://logo-petr4"
                }
              ]
            }
            """)
        }));
        var sut = CreateSut(handler);

        var result = await sut.GetProfileAsync("petr4", CancellationToken.None);

        result.Symbol.Should().Be("PETR4");
        result.Name.Should().Be("Petro");
        result.LogoUrl.Should().Be("https://logo-petr4");
    }

    [Fact]
    public async Task GetHistoryAsync_Should_Fallback_To_Default_Period_When_Invalid()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "results": [
                {
                  "symbol": "VALE3",
                  "regularMarketPrice": 55.4,
                  "regularMarketChangePercent": -0.5
                }
              ]
            }
            """)
        }));

        var sut = CreateSut(handler);

        var result = await sut.GetHistoryAsync("vale3", "invalid-period", CancellationToken.None);

        result.Period.Should().Be("6mo");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("range=6mo");
    }

    [Fact]
    public async Task GetSnapshotsAsync_Should_Return_Empty_When_Brapi_Returns_Error_Node()
    {
        var handler = new CaptureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "error": true,
              "message": "token inválido"
            }
            """)
        }));
        var sut = CreateSut(handler);

        var result = await sut.GetSnapshotsAsync(["PETR4"], CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static FreeMarketDataProvider CreateSut(HttpMessageHandler handler, string? token = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://brapi.dev/")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("MarketBrapi")).Returns(client);

        return new FreeMarketDataProvider(
            factory.Object,
            Options.Create(new MarketDataOptions { BrapiToken = token }),
            NullLogger<FreeMarketDataProvider>.Instance);
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return callback(request);
        }
    }
}
