using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class FeatureAuthorizationIntegrationTests
{
    [Fact]
    public async Task Basic_Should_Be_Able_To_List_Accounts()
    {
        await using var host = await FeatureAuthorizationTestHost.StartAsync(UserRole.Basic);
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Basic_Should_Be_Able_To_List_Accounts_With_Roles_Claim()
    {
        await using var host = await FeatureAuthorizationTestHost.StartAsync(UserRole.Basic, "roles");
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Basic_Should_Be_Able_To_Create_Cards()
    {
        await using var host = await FeatureAuthorizationTestHost.StartAsync(UserRole.Basic);
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cards")
        {
            Content = JsonContent.Create(new CardRequest(1, "User", "Cartao", "1234", null, 1000m, 10, 20))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Basic_Should_Return_Forbidden_For_Installment_Anticipation()
    {
        await using var host = await FeatureAuthorizationTestHost.StartAsync(UserRole.Basic);
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/installments/{Guid.NewGuid()}/anticipations")
        {
            Content = JsonContent.Create(new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow)))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Intermediate_Should_Return_Forbidden_For_Investments()
    {
        await using var host = await FeatureAuthorizationTestHost.StartAsync(UserRole.Intermediate);
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/investments/goal");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed class FeatureAuthorizationTestHost(WebApplication app) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public static async Task<FeatureAuthorizationTestHost> StartAsync(UserRole role, string roleClaimType = ClaimTypes.Role)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
            var accountsService = BuildAccountsService();
            builder.Services.AddSingleton(accountsService);
            builder.Services.AddSingleton<IAccountQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            builder.Services.AddSingleton<IAccountCommandService>(sp => sp.GetRequiredService<IAccountsService>());
            builder.Services.AddSingleton<IAccountTransactionQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            builder.Services.AddSingleton<IAccountTransferService>(sp => sp.GetRequiredService<IAccountsService>());
            builder.Services.AddSingleton(Mock.Of<ICardsService>());
            builder.Services.AddSingleton(Mock.Of<IInstallmentsService>());
            builder.Services.AddSingleton(Mock.Of<IAuditService>());
            var investmentsService = BuildInvestmentsService();
            builder.Services.AddSingleton(investmentsService);
            builder.Services.AddSingleton<IInvestmentGoalQueryService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentGoalCommandService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentAllocationQueryService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentAllocationCommandService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentPositionQueryService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentPositionCommandService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentMarketEnrichmentService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton(Mock.Of<IInvestmentsApplicationService>());
            builder.Services.AddSingleton(Mock.Of<IInvestmentBenchmarksService>());
            builder.Services.AddSingleton(Mock.Of<IB3SyncService>());
            builder.Services.AddSingleton(Mock.Of<IOfxImportService>());
            builder.Services.AddSingleton(Mock.Of<ICsvImportService>());

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestAuth";
                    options.DefaultChallengeScheme = "TestAuth";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options =>
                {
                    options.ClaimsIssuer = roleClaimType + "|" + role.ToString();
                });

            builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder("TestAuth")
                    .RequireAuthenticatedUser()
                    .Build());
            builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);

            var app = builder.Build();
            app.UseGlobalProblemDetails(includeExceptionDetails: false);
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();

            return new FeatureAuthorizationTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
        }

        private static IAccountsService BuildAccountsService()
        {
            var mock = new Mock<IAccountsService>();
            mock.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AccountResponse>());
            return mock.Object;
        }

        private static IInvestmentsService BuildInvestmentsService()
        {
            var mock = new Mock<IInvestmentsService>();
            mock.Setup(x => x.GetGoalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvestmentGoalDto?)null);
            return mock.Object;
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var roleClaimParts = (Options.ClaimsIssuer ?? $"{ClaimTypes.Role}|{UserRole.Basic}")
                .Split('|', 2, StringSplitOptions.TrimEntries);
            var roleClaimType = roleClaimParts.Length > 0 && !string.IsNullOrWhiteSpace(roleClaimParts[0])
                ? roleClaimParts[0]
                : ClaimTypes.Role;
            var role = roleClaimParts.Length > 1 && !string.IsNullOrWhiteSpace(roleClaimParts[1])
                ? roleClaimParts[1]
                : UserRole.Basic.ToString();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.Parse("11111111-1111-1111-1111-111111111111").ToString()),
                new Claim(roleClaimType, role)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
