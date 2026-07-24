using System.Security.Claims;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace InvestindoEmNegocio.Extensions;

public static class ApplicationPipelineExtensions
{
    public static WebApplication MapApiDocs(this WebApplication app)
    {
        app.MapOpenApi("/openapi/v1.json");
        app.MapScalarApiReference("/docs", options =>
        {
            options.Title = "InvestindoEmNegocio API";
            options.Theme = ScalarTheme.BluePlanet;
            options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });

        return app;
    }

    public static WebApplication UseAppPipeline(this WebApplication app, string corsPolicy)
    {
        app.UseGlobalProblemDetails(app.Environment.IsDevelopment());
        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.UseHttpsRedirection();
        app.UseSecurityHeaders();
        app.UseResponseCompression();
        app.UseStaticFiles();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            // AppProblemException com status < 500 é erro de NEGÓCIO esperado (ex.: 409
            // "cartão já existe", 400 validação, 404) — o cliente recebe o status certo
            // via UseGlobalProblemDetails. Não classificar como Error para não poluir os
            // logs/alertas com falso "500". Falhas reais (5xx / outras exceções) seguem Error.
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex is AppProblemException appProblem && appProblem.StatusCode < StatusCodes.Status500InternalServerError)
                    return LogEventLevel.Warning;
                if (ex is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                    return LogEventLevel.Error;
                return LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
                var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User?.FindFirstValue(ClaimTypes.Name);
                if (!string.IsNullOrWhiteSpace(userId))
                    diagnosticContext.Set("UserId", userId);
            };
        });

        app.UseRouting();
        app.UseCors(corsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();

        return app;
    }

    private static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // CSP só na API JSON; /docs e /openapi usam scripts da Scalar UI que a política quebraria.
            if (!context.Request.Path.StartsWithSegments("/docs") &&
                !context.Request.Path.StartsWithSegments("/openapi"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            await next();
        });

        return app;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true
        }).AllowAnonymous();

        app.MapHealthChecks("/health/db", new HealthCheckOptions
        {
            Predicate = check => check.Name == "db"
        }).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Name == "self"
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Name == "db"
        }).AllowAnonymous();

        return app;
    }

    public static void LogOtelConfiguration(this WebApplication app, OtlpRuntimeSettings settings)
    {
        Log.Information("OTEL config loaded. Endpoint: {OtelEndpoint}, Protocol: {OtelProtocol}, ServiceName: {OtelServiceName}",
            settings.Endpoint?.ToString() ?? "<not-set>", settings.Protocol, settings.ServiceName);
    }
}
