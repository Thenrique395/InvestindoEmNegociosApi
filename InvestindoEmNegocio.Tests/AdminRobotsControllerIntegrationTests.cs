using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class AdminRobotsControllerIntegrationTests
{
    [Fact]
    public async Task Monitor_Should_Return_Forbidden_When_User_Is_Not_Admin()
    {
        await using var app = await BuildTestAppAsync(isAdmin: false);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/robots/monitor");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await app.GetTestClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Monitor_Should_Return_Ok_When_User_Is_Admin()
    {
        await using var app = await BuildTestAppAsync(isAdmin: true);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/robots/monitor?take=10");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await app.GetTestClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<RobotMonitorResponseDto>();
        payload.Should().NotBeNull();
    }

    private static async Task<WebApplication> BuildTestAppAsync(bool isAdmin)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddControllers().AddApplicationPart(typeof(AdminRobotsController).Assembly);
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("admin-robots", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
            });
        });

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestAuth";
                options.DefaultChallengeScheme = "TestAuth";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options =>
            {
                options.ClaimsIssuer = isAdmin ? "admin" : "basic";
            });

        builder.Services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder("TestAuth")
                .RequireAuthenticatedUser()
                .Build());

        builder.Services.AddSingleton<IAdminRobotsService>(new FakeAdminRobotsService());

        var app = builder.Build();
        app.UseGlobalProblemDetails(includeExceptionDetails: false);
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var issuer = Options.ClaimsIssuer ?? "basic";
            var role = issuer == "admin" ? "Admin" : "Basic";

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.Parse("11111111-1111-1111-1111-111111111111").ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakeAdminRobotsService : IAdminRobotsService
    {
        public Task<RobotMonitorResponseDto> MonitorAsync(RobotMonitorQueryDto query, CancellationToken cancellationToken = default)
        {
            var response = new RobotMonitorResponseDto(
                new RobotMonitorSummaryDto(0, 0, 0, 0, 0, 0, 0, 0),
                [],
                []);
            return Task.FromResult(response);
        }

        public Task<RobotRunResultDto?> RunAsync(string robotName, bool force, int cooldownMinutes, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
        {
            RobotRunResultDto result = new(
                robotName,
                DateTime.UtcNow,
                DateTime.UtcNow,
                0,
                "corr",
                "host",
                triggeredByUserId,
                true,
                0,
                new RobotExecutionMetricsDto(0, 0, 0, 0, "NO_NEW_NOTIFICATIONS"),
                false,
                null,
                null);
            return Task.FromResult<RobotRunResultDto?>(result);
        }

        public Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RobotRunResultDto>>([]);
    }
}
