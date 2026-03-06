using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class ProfileControllerIntegrationTests
{
    [Fact]
    public async Task Upsert_Then_Get_Should_Persist_And_Return_FinancialGoal()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profile")
        {
            Content = JsonContent.Create(new
            {
                fullName = "Henrique Santos",
                document = "12345678901",
                phone = "(81) 99525-7823",
                birthDate = "1990-01-01T00:00:00Z",
                avatarUrl = "",
                city = "Recife",
                state = "PE",
                country = "Brasil",
                financialGoal = "comecar-investir",
                language = "pt-BR",
                carryOverDay = 12,
                intelligenceMode = "C"
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var upsertResponse = await client.SendAsync(request);
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var upsertPayload = await upsertResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        upsertPayload.Should().NotBeNull();
        upsertPayload!.FinancialGoal.Should().Be("comecar-investir");
        upsertPayload!.CarryOverDay.Should().Be(12);
        upsertPayload!.IntelligenceMode.Should().Be("C");

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/profile");
        getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var getResponse = await client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getPayload = await getResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        getPayload.Should().NotBeNull();
        getPayload!.FinancialGoal.Should().Be("comecar-investir");
        getPayload!.CarryOverDay.Should().Be(12);
        getPayload!.IntelligenceMode.Should().Be("C");
    }

    private static async Task<WebApplication> BuildTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddControllers().AddApplicationPart(typeof(ProfileController).Assembly);
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddSingleton<IAvatarStorageService, FakeAvatarStorageService>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestAuth";
                options.DefaultChallengeScheme = "TestAuth";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", _ => { });

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
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.Parse("11111111-1111-1111-1111-111111111111").ToString()),
                new Claim(ClaimTypes.Role, "Basic")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class InMemoryUserProfileRepository : IUserProfileRepository
    {
        private readonly Dictionary<Guid, UserProfile> _profiles = new();

        public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _profiles.TryGetValue(userId, out var profile);
            return Task.FromResult(profile);
        }

        public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
        {
            _profiles[profile.UserId] = profile;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAvatarStorageService : IAvatarStorageService
    {
        public Task<string> SaveAsync(
            Guid userId,
            Stream content,
            string fileName,
            string contentType,
            string baseUrl,
            CancellationToken cancellationToken)
            => Task.FromResult($"{baseUrl}/uploads/{userId}/avatar.png");
    }
}
