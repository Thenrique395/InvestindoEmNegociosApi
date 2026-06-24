using System.Net;
using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Confirma que o limiter "billing-checkout" (Extensions/ServiceCollectionExtensions.cs)
/// realmente bloqueia com 429 a partir do 6º request no mesmo minuto, e que é particionado
/// por usuário — não um contador global que um usuário esgotaria para os outros.
/// </summary>
public class BillingCheckoutRateLimitTests
{
    [Fact]
    public async Task Sixth_Checkout_Request_Within_Window_Should_Be_Rejected_With_429()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "user-a");

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsync("/test/checkout", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"a requisição {i + 1} está dentro do limite de 5/min");
        }

        var sixth = await client.PostAsync("/test/checkout", null);
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Rate_Limit_Should_Be_Partitioned_Per_User_Not_Global()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "user-a");
        for (var i = 0; i < 5; i++)
            await client.PostAsync("/test/checkout", null);
        (await client.PostAsync("/test/checkout", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "usuário A já esgotou a própria cota");

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "user-b");
        var responseForUserB = await client.PostAsync("/test/checkout", null);
        responseForUserB.StatusCode.Should().Be(HttpStatusCode.OK, "usuário B tem sua própria cota, não compartilha com o usuário A");
    }

    private static async Task<WebApplication> BuildTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAppRateLimiting();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestAuth";
                options.DefaultChallengeScheme = "TestAuth";
            })
            .AddScheme<AuthenticationSchemeOptions, BearerAsUserIdAuthHandler>("TestAuth", _ => { });
        builder.Services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder("TestAuth").RequireAuthenticatedUser().Build());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapPost("/test/checkout", () => Results.Ok())
            .RequireAuthorization()
            .RequireRateLimiting("billing-checkout");

        await app.StartAsync();
        return app;
    }

    /// <summary>Lê o valor literal do header Authorization (sem validação) como o NameIdentifier do usuário.</summary>
    private sealed class BearerAsUserIdAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header) || string.IsNullOrWhiteSpace(header))
                return Task.FromResult(AuthenticateResult.NoResult());

            var userId = header.ToString().Replace("Bearer ", string.Empty);
            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)],
                "TestAuth");
            var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), "TestAuth");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
