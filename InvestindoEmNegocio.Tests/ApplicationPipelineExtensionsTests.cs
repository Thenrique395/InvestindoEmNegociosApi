using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvestindoEmNegocio.Tests;

public class ApplicationPipelineExtensionsTests
{
    [Fact]
    public void MapHealthEndpoints_Should_Register_All_Expected_Routes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication();
        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.MapHealthEndpoints();

        var endpointBuilder = (IEndpointRouteBuilder)app;
        var routes = endpointBuilder.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(x => x.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/health");
        routes.Should().Contain("/health/db");
        routes.Should().Contain("/health/live");
        routes.Should().Contain("/health/ready");
    }

    [Fact]
    public void MapApiDocs_Should_Map_OpenApi_And_Scalar_Endpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApiSurface(isDevelopment: true);

        var app = builder.Build();

        app.MapApiDocs();

        var endpointBuilder = (IEndpointRouteBuilder)app;
        var routes = endpointBuilder.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(x => x.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("/openapi/", StringComparison.Ordinal));
        routes.Should().Contain(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("/docs", StringComparison.Ordinal));
    }

    [Fact]
    public void UseAppPipeline_Should_Register_Middleware_Without_Throwing()
    {
        var builder = WebApplication.CreateBuilder();
        var configuration = new ConfigurationBuilder().Build();
        builder.Services
            .AddApiSurface(isDevelopment: true)
            .AddAppCors("AllowFrontend", isDevelopment: true, configuration)
            .AddAppRateLimiting();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        Action act = () => app.UseAppPipeline("AllowFrontend");

        act.Should().NotThrow();
    }

    [Fact]
    public void MapHealthEndpoints_Should_Return_Same_Builder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication();
        builder.Services.AddHealthChecks();
        var app = builder.Build();

        var result = app.MapHealthEndpoints();

        result.Should().BeSameAs((IEndpointRouteBuilder)app);
    }

    [Fact]
    public void LogOtelConfiguration_Should_Not_Throw()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        var settings = new OtlpRuntimeSettings(new Uri("http://localhost:4318"), OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf, "service-test");

        var act = () => app.LogOtelConfiguration(settings);

        act.Should().NotThrow();
    }
}
