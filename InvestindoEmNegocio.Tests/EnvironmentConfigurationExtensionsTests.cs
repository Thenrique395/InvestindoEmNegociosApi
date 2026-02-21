using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace InvestindoEmNegocio.Tests;

[CollectionDefinition("EnvVars", DisableParallelization = true)]
public sealed class EnvVarsCollection;

[Collection("EnvVars")]
public class EnvironmentConfigurationExtensionsTests
{
    [Fact]
    public void LoadEnvironmentVariablesFromConfiguration_Should_Set_Env_When_Not_Already_Set()
    {
        var keys = new[]
        {
            "Jwt__Issuer",
            "Jwt__Audience",
            "ConnectionStrings__Default"
        };
        var snapshot = Capture(keys);

        try
        {
            Environment.SetEnvironmentVariable("Jwt__Issuer", null);
            Environment.SetEnvironmentVariable("Jwt__Audience", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);

            using var tempDir = new TemporaryDirectory();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = tempDir.Path
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer-from-config",
                ["Jwt:Audience"] = "audience-from-config",
                ["ConnectionStrings:Default"] = "Host=localhost;Database=test;"
            });

            builder.LoadEnvironmentVariablesFromConfiguration();

            Environment.GetEnvironmentVariable("Jwt__Issuer").Should().Be("issuer-from-config");
            Environment.GetEnvironmentVariable("Jwt__Audience").Should().Be("audience-from-config");
            Environment.GetEnvironmentVariable("ConnectionStrings__Default").Should().Be("Host=localhost;Database=test;");
        }
        finally
        {
            Restore(snapshot);
        }
    }

    [Fact]
    public void LoadEnvironmentVariablesFromConfiguration_Should_Not_Override_Existing_Env()
    {
        const string key = "Jwt__Issuer";
        var previous = Environment.GetEnvironmentVariable(key);

        try
        {
            Environment.SetEnvironmentVariable(key, "issuer-from-env");

            using var tempDir = new TemporaryDirectory();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = tempDir.Path
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer-from-config"
            });

            builder.LoadEnvironmentVariablesFromConfiguration();

            Environment.GetEnvironmentVariable(key).Should().Be("issuer-from-env");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    private static Dictionary<string, string?> Capture(IEnumerable<string> keys)
    {
        var snapshot = new Dictionary<string, string?>();
        foreach (var key in keys)
        {
            snapshot[key] = Environment.GetEnvironmentVariable(key);
        }

        return snapshot;
    }

    private static void Restore(Dictionary<string, string?> snapshot)
    {
        foreach (var (key, value) in snapshot)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inv-negocio-tests-{Guid.NewGuid():N}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
