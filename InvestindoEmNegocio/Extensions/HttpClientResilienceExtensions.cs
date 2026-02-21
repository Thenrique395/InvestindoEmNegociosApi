using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace InvestindoEmNegocio.Extensions;

public static class HttpClientResilienceExtensions
{
    public static IHttpClientBuilder AddExternalApiResilience(
        this IHttpClientBuilder builder,
        TimeSpan timeoutPerTry,
        int retryCount,
        TimeSpan baseDelay,
        int breakerHandledEventsAllowedBeforeBreaking,
        TimeSpan breakerDuration)
    {
        return builder.AddPolicyHandler(CreateRetryPolicy(retryCount, baseDelay))
            .AddPolicyHandler(CreateCircuitBreakerPolicy(
                breakerHandledEventsAllowedBeforeBreaking,
                breakerDuration))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(timeoutPerTry));
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(int retryCount, TimeSpan baseDelay)
    {
        return HttpPolicyExtensions.HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(retryCount, attempt =>
            {
                var jitterMs = Random.Shared.Next(50, 250);
                var expo = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                return expo + TimeSpan.FromMilliseconds(jitterMs);
            });
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(int handledEventsAllowedBeforeBreaking, TimeSpan durationOfBreak)
    {
        return HttpPolicyExtensions.HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking, durationOfBreak);
    }
}
