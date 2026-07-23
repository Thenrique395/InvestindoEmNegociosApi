using System.Net;
using System.Net.Http;
using FluentAssertions;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// O limiter "auth" (login/cadastro/senha) deve ser particionado por IP — não global. Um limiter
/// global bloquearia usuários legítimos assim que o sistema INTEIRO passasse do limite no minuto
/// (o 6º login de qualquer pessoa → 429). Aqui cada IP tem a própria cota.
/// </summary>
public class AuthRateLimitTests
{
    [Fact]
    public async Task Auth_Limiter_Blocks_After_The_Limit_For_The_Same_Ip()
    {
        await using var app = await BuildAsync();
        var client = app.GetTestClient();

        for (var i = 0; i < 20; i++)
            (await Get(client, "9.9.9.1")).StatusCode.Should().Be(HttpStatusCode.OK, $"req {i + 1} está dentro do limite de 20/min");

        (await Get(client, "9.9.9.1")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Auth_Limiter_Is_Partitioned_Per_Ip_Not_Global()
    {
        await using var app = await BuildAsync();
        var client = app.GetTestClient();

        for (var i = 0; i < 20; i++)
            await Get(client, "10.0.0.1");
        (await Get(client, "10.0.0.1")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "o IP A esgotou a própria cota");

        (await Get(client, "10.0.0.2")).StatusCode.Should().Be(HttpStatusCode.OK, "o IP B tem cota própria — não compartilha com o IP A");
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/auth");
        request.Headers.Add("X-Test-Ip", ip);
        return client.SendAsync(request);
    }

    private static async Task<WebApplication> BuildAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAppRateLimiting();

        var app = builder.Build();
        // Simula o IP do cliente por header antes do rate limiter (no servidor real vem da conexão).
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Headers.TryGetValue("X-Test-Ip", out var ip) &&
                System.Net.IPAddress.TryParse(ip.ToString(), out var parsed))
            {
                ctx.Connection.RemoteIpAddress = parsed;
            }
            await next();
        });
        app.UseRateLimiter();
        app.MapGet("/test/auth", () => Results.Ok()).RequireRateLimiting("auth");

        await app.StartAsync();
        return app;
    }
}
