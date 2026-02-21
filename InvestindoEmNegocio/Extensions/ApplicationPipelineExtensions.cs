using System.Security.Claims;
using InvestindoEmNegocio.Infrastructure.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

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
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseStaticFiles();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
                var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User?.FindFirstValue(ClaimTypes.Name);
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }
            };
        });

        app.UseRouting();
        app.UseCors(corsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

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
        Log.Information(
            "OTEL config loaded. Endpoint: {OtelEndpoint}, Protocol: {OtelProtocol}, ServiceName: {OtelServiceName}",
            settings.Endpoint?.ToString() ?? "<not-set>",
            settings.Protocol,
            settings.ServiceName);
    }
}
