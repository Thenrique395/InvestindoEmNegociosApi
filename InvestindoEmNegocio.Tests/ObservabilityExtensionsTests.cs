using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Exporter;

namespace InvestindoEmNegocio.Tests;

[Collection("EnvVars")]
public class ObservabilityExtensionsTests
{
    [Fact]
    public void AddAppObservability_Should_Use_Defaults_When_Env_Not_Set()
    {
        var snapshot = Capture(
            "OTEL_SERVICE_NAME",
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_PROTOCOL");

        try
        {
            Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", null);

            var builder = WebApplication.CreateBuilder();

            var settings = builder.AddAppObservability();

            settings.ServiceName.Should().Be("InvestindoEmNegocio");
            settings.Endpoint.Should().BeNull();
            settings.Protocol.Should().Be(OtlpExportProtocol.Grpc);
        }
        finally
        {
            Restore(snapshot);
        }
    }

    [Fact]
    public void AddAppObservability_Should_Respect_Env_Values()
    {
        var snapshot = Capture(
            "OTEL_SERVICE_NAME",
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_PROTOCOL");

        try
        {
            Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "investindo-tests");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4318");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

            var builder = WebApplication.CreateBuilder();

            var settings = builder.AddAppObservability();

            settings.ServiceName.Should().Be("investindo-tests");
            settings.Endpoint.Should().Be(new Uri("http://localhost:4318"));
            settings.Protocol.Should().Be(OtlpExportProtocol.HttpProtobuf);
        }
        finally
        {
            Restore(snapshot);
        }
    }

    [Fact]
    public void AddAppObservability_Should_Fallback_To_Grpc_For_Invalid_Protocol()
    {
        var snapshot = Capture("OTEL_EXPORTER_OTLP_PROTOCOL");

        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "invalid-protocol");
            var builder = WebApplication.CreateBuilder();

            var settings = builder.AddAppObservability();

            settings.Protocol.Should().Be(OtlpExportProtocol.Grpc);
        }
        finally
        {
            Restore(snapshot);
        }
    }

    [Fact]
    public void AddAppObservability_Should_Ignore_Invalid_Endpoint()
    {
        var snapshot = Capture("OTEL_EXPORTER_OTLP_ENDPOINT");

        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "not-a-valid-uri");
            var builder = WebApplication.CreateBuilder();

            var settings = builder.AddAppObservability();

            settings.Endpoint.Should().BeNull();
        }
        finally
        {
            Restore(snapshot);
        }
    }

    [Fact]
    public void AddAppObservability_Should_Use_Grpc_When_Configured()
    {
        var snapshot = Capture("OTEL_EXPORTER_OTLP_PROTOCOL");

        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
            var builder = WebApplication.CreateBuilder();

            var settings = builder.AddAppObservability();

            settings.Protocol.Should().Be(OtlpExportProtocol.Grpc);
        }
        finally
        {
            Restore(snapshot);
        }
    }

    private static Dictionary<string, string?> Capture(params string[] keys)
    {
        return keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
    }

    private static void Restore(Dictionary<string, string?> snapshot)
    {
        foreach (var (key, value) in snapshot)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
