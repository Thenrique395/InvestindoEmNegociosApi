using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace InvestindoEmNegocio.Extensions;

public static class ObservabilityExtensions
{
    public static OtlpRuntimeSettings AddAppObservability(this WebApplicationBuilder builder)
    {
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "InvestindoEmNegocio";
        var endpointValue = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var protocolValue = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        var endpoint = ResolveOtlpEndpoint(endpointValue);
        var protocol = ResolveOtlpProtocol(protocolValue);

        var resource = ResourceBuilder.CreateDefault().AddService(serviceName);

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "InvestindoEmNegocio")
                .WriteTo.Console(new RenderedCompactJsonFormatter());

            if (endpoint is not null)
            {
                configuration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = endpoint.ToString();
                    options.Protocol = protocol == OtlpExportProtocol.Grpc
                        ? OtlpProtocol.Grpc
                        : OtlpProtocol.HttpProtobuf;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName
                    };
                });
            }
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    if (endpoint is not null)
                        options.Endpoint = endpoint;

                    options.Protocol = protocol;
                }))
            .WithMetrics(metricProviderBuilder => metricProviderBuilder
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options =>
                {
                    if (endpoint is not null)
                        options.Endpoint = endpoint;

                    options.Protocol = protocol;
                }));

        return new OtlpRuntimeSettings(endpoint, protocol, serviceName);
    }

    private static OtlpExportProtocol ResolveOtlpProtocol(string? protocolValue)
    {
        if (string.IsNullOrWhiteSpace(protocolValue))
            return OtlpExportProtocol.Grpc;

        return protocolValue.Trim().ToLowerInvariant() switch
        {
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            "grpc" => OtlpExportProtocol.Grpc,
            _ => OtlpExportProtocol.Grpc
        };
    }

    private static Uri? ResolveOtlpEndpoint(string? endpointValue)
    {
        if (string.IsNullOrWhiteSpace(endpointValue))
            return null;

        return Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ? endpoint : null;
    }
}

public sealed record OtlpRuntimeSettings(Uri? Endpoint, OtlpExportProtocol Protocol, string ServiceName);
