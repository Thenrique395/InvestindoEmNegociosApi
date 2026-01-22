using System.Diagnostics;

namespace InvestindoEmNegocio.Infrastructure.Logging;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "x-correlation-id";
    private const string RequestIdHeaderName = "x-request-id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return headerValue.ToString();
        }

        if (context.Request.Headers.TryGetValue(RequestIdHeaderName, out var requestIdHeaderValue))
        {
            return requestIdHeaderValue.ToString();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
