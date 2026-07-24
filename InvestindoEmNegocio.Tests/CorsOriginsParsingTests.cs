using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.Extensions.Configuration;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Regressão do CORS: a env var Cors__AllowedOrigins vem como UMA string separada por
/// vírgula. O binder .Get&lt;string[]&gt;() não divide isso sozinho, então a lista ficava
/// vazia e só o loopback passava — bloqueando o frontend do VPS. ParseCorsOrigins deve
/// dividir a vírgula (e ainda suportar a forma indexada).
/// </summary>
public class CorsOriginsParsingTests
{
    [Fact]
    public void Should_Split_CommaSeparated_EnvVar_Form()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins"] = "http://35.174.50.187:4201, http://35.174.50.187:4200 ,http://localhost:4200"
            })
            .Build();

        var origins = ServiceCollectionExtensions.ParseCorsOrigins(config);

        origins.Should().BeEquivalentTo(
            "http://35.174.50.187:4201",
            "http://35.174.50.187:4200",
            "http://localhost:4200");
    }

    [Fact]
    public void Should_Support_Indexed_Form()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "http://a.com",
                ["Cors:AllowedOrigins:1"] = "http://b.com"
            })
            .Build();

        var origins = ServiceCollectionExtensions.ParseCorsOrigins(config);

        origins.Should().BeEquivalentTo("http://a.com", "http://b.com");
    }

    [Fact]
    public void Should_Return_Empty_When_Unset()
    {
        var config = new ConfigurationBuilder().Build();

        ServiceCollectionExtensions.ParseCorsOrigins(config).Should().BeEmpty();
    }
}
