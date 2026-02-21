using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;

namespace InvestindoEmNegocio.Tests;

public class HttpClientResilienceExtensionsTests
{
    [Fact]
    public async Task AddExternalApiResilience_Should_Retry_On_Transient_Failures()
    {
        var handler = new SequenceHandler(
        [
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        ]);

        var services = new ServiceCollection();
        services
            .AddHttpClient("resilient-retry")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddExternalApiResilience(
                timeoutPerTry: TimeSpan.FromSeconds(3),
                retryCount: 2,
                baseDelay: TimeSpan.FromMilliseconds(1),
                breakerHandledEventsAllowedBeforeBreaking: 10,
                breakerDuration: TimeSpan.FromSeconds(30));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("resilient-retry");

        var response = await client.GetAsync("http://example.test/retry");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task AddExternalApiResilience_Should_Open_Circuit_After_Configured_Failures()
    {
        var handler = new SequenceHandler([
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        ]);

        var services = new ServiceCollection();
        services
            .AddHttpClient("resilient-breaker")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddExternalApiResilience(
                timeoutPerTry: TimeSpan.FromSeconds(3),
                retryCount: 0,
                baseDelay: TimeSpan.FromMilliseconds(1),
                breakerHandledEventsAllowedBeforeBreaking: 1,
                breakerDuration: TimeSpan.FromSeconds(30));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("resilient-breaker");

        var first = await client.GetAsync("http://example.test/breaker-first");
        first.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);

        Func<Task> secondCall = async () => await client.GetAsync("http://example.test/breaker-second");

        await secondCall.Should().ThrowAsync<BrokenCircuitException>();
        handler.CallCount.Should().Be(1);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

        public int CallCount { get; private set; }

        public SequenceHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }

            var responseFactory = _responses.Dequeue();
            return Task.FromResult(responseFactory(request));
        }
    }
}
