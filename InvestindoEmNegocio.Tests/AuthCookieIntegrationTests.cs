using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Exercita o pipeline real de autenticação por cookie httpOnly (login/refresh/logout,
/// JwtBearer lendo cookie, CSRF) de ponta a ponta via TestServer — sem precisar de browser
/// real nem de Postgres, já que é exatamente o protocolo (SameSite/Secure/HttpOnly/CSRF) que
/// causou a reversão da tentativa anterior.
/// </summary>
public class AuthCookieIntegrationTests
{
    private const string JwtSecret = "test-secret-key-pelo-menos-32-caracteres-1234567890";

    [Fact]
    public async Task Login_Should_Set_HttpOnly_SameSiteNone_Secure_Cookies_And_Not_Return_Token_In_Body()
    {
        await using var app = await BuildTestAppAsync();

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        body.Should().NotBeNull();
        body!.Role.Should().Be("Basic");

        var rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("\"token\"", "o JWT não deve mais ser retornado no corpo da resposta");
        rawBody.Should().NotContain("\"refreshToken\"", "o refresh token não deve mais ser retornado no corpo da resposta");

        var cookies = ParseSetCookies(response);
        cookies.Should().ContainKey(AuthCookieService.AccessTokenCookie);
        cookies.Should().ContainKey(AuthCookieService.RefreshTokenCookie);
        cookies.Should().ContainKey(AuthCookieService.CsrfCookie);

        cookies[AuthCookieService.AccessTokenCookie].HttpOnly.Should().BeTrue();
        cookies[AuthCookieService.AccessTokenCookie].Secure.Should().BeTrue();
        cookies[AuthCookieService.AccessTokenCookie].SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.None);

        cookies[AuthCookieService.RefreshTokenCookie].HttpOnly.Should().BeTrue();
        cookies[AuthCookieService.RefreshTokenCookie].Secure.Should().BeTrue();
        cookies[AuthCookieService.RefreshTokenCookie].SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.None);

        // XSRF-TOKEN precisa ser legível via JS para o frontend reenviar no header.
        cookies[AuthCookieService.CsrfCookie].HttpOnly.Should().BeFalse();
        cookies[AuthCookieService.CsrfCookie].Secure.Should().BeTrue();
        cookies[AuthCookieService.CsrfCookie].SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.None);
    }

    [Fact]
    public async Task Authenticated_Request_Should_Succeed_Using_Only_Cookie_No_Authorization_Header()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var request = new HttpRequestMessage(HttpMethod.Get, "/test/protected");
        request.Headers.Add("Cookie", $"{AuthCookieService.AccessTokenCookie}={cookies[AuthCookieService.AccessTokenCookie].Value}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        request.Headers.Authorization.Should().BeNull("não deve depender de header Authorization quando o cookie está presente");
    }

    [Fact]
    public async Task Authenticated_Request_Should_Fail_After_Session_Revoked_Even_With_Still_Unexpired_Cookie()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var beforeRevoke = new HttpRequestMessage(HttpMethod.Get, "/test/protected");
        beforeRevoke.Headers.Add("Cookie", $"{AuthCookieService.AccessTokenCookie}={cookies[AuthCookieService.AccessTokenCookie].Value}");
        (await client.SendAsync(beforeRevoke)).StatusCode.Should().Be(HttpStatusCode.OK, "o token recém-emitido deve ser válido");

        FakeAuthAccessApplicationService.RevokeTestUserSessions();

        var afterRevoke = new HttpRequestMessage(HttpMethod.Get, "/test/protected");
        afterRevoke.Headers.Add("Cookie", $"{AuthCookieService.AccessTokenCookie}={cookies[AuthCookieService.AccessTokenCookie].Value}");
        var response = await client.SendAsync(afterRevoke);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "o MESMO cookie antigo (não expirado) não deve mais funcionar depois da revogação");
    }

    [Fact]
    public async Task Refresh_Should_Read_RefreshToken_From_Cookie_Not_Body()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", $"{AuthCookieService.RefreshTokenCookie}={cookies[AuthCookieService.RefreshTokenCookie].Value}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedCookies = ParseSetCookies(response);
        refreshedCookies.Should().ContainKey(AuthCookieService.AccessTokenCookie);
    }

    [Fact]
    public async Task Refresh_Without_RefreshToken_Cookie_Should_Return_Unauthorized()
    {
        await using var app = await BuildTestAppAsync();

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/refresh", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Clear_Auth_Cookies()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{AuthCookieService.RefreshTokenCookie}={cookies[AuthCookieService.RefreshTokenCookie].Value}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var clearedCookies = ParseSetCookies(response);
        clearedCookies[AuthCookieService.AccessTokenCookie].Expires.Should().BeBefore(DateTimeOffset.UtcNow);
        clearedCookies[AuthCookieService.RefreshTokenCookie].Expires.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Mutating_Request_Without_Csrf_Header_Should_Be_Rejected_When_Csrf_Cookie_Present()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "/test/mutating");
        request.Headers.Add(
            "Cookie",
            $"{AuthCookieService.AccessTokenCookie}={cookies[AuthCookieService.AccessTokenCookie].Value}; {AuthCookieService.CsrfCookie}={cookies[AuthCookieService.CsrfCookie].Value}");
        // Propositalmente sem o header X-XSRF-TOKEN.

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mutating_Request_With_Matching_Csrf_Header_Should_Succeed()
    {
        await using var app = await BuildTestAppAsync();
        var client = app.GetTestClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "123456"));
        var cookies = ParseSetCookies(loginResponse);

        var request = new HttpRequestMessage(HttpMethod.Post, "/test/mutating");
        request.Headers.Add(
            "Cookie",
            $"{AuthCookieService.AccessTokenCookie}={cookies[AuthCookieService.AccessTokenCookie].Value}; {AuthCookieService.CsrfCookie}={cookies[AuthCookieService.CsrfCookie].Value}");
        request.Headers.Add(AuthCookieService.CsrfHeader, cookies[AuthCookieService.CsrfCookie].Value.ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Dictionary<string, SetCookieHeaderValue> ParseSetCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var rawCookies))
            return new Dictionary<string, SetCookieHeaderValue>();

        return rawCookies
            .Select(raw => SetCookieHeaderValue.Parse(raw))
            .ToDictionary(c => c.Name.ToString(), c => c);
    }

    private static async Task<WebApplication> BuildTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "InvestindoEmNegocio",
            ["Jwt:Audience"] = "InvestindoEmNegocio",
            ["Jwt:SecretKey"] = JwtSecret,
            ["Jwt:ExpiresMinutes"] = "15"
        });

        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
            });
        });

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.Services.AddSingleton<IAuthCookieService, AuthCookieService>();
        builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        builder.Services.AddSingleton<IAuthAvailabilityService>(new FakeAuthAvailabilityService());
        builder.Services.AddSingleton<IAuthAccessApplicationService>(sp =>
            new FakeAuthAccessApplicationService(sp.GetRequiredService<IJwtTokenGenerator>()));
        // OnTokenValidated (revogação imediata de sessão) resolve IUserRepository por
        // requisição — sem isso, toda requisição autenticada deste host de teste falharia.
        builder.Services.AddSingleton<IUserRepository, FakeUserRepository>();

        var app = builder.Build();
        app.UseGlobalProblemDetails(includeExceptionDetails: false);
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/test/protected", () => Results.Ok()).RequireAuthorization();
        app.MapPost("/test/mutating", () => Results.Ok()).RequireAuthorization();

        await app.StartAsync();
        return app;
    }

    private sealed class FakeAuthAvailabilityService : IAuthAvailabilityService
    {
        public Task<CheckAvailabilityResponse> CheckAsync(CheckAvailabilityRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CheckAvailabilityResponse(false, false));
    }

    /// <summary>
    /// Reflete o mesmo usuário fake (estático, compartilhado por todos os testes deste arquivo)
    /// usado para emitir os JWTs — é nele que o OnTokenValidated busca IsActive/TokenVersion.
    /// </summary>
    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(FakeAuthAccessApplicationService.TestUser);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(FakeAuthAccessApplicationService.TestUser.Id == id ? FakeAuthAccessApplicationService.TestUser : null);

        public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>([FakeAuthAccessApplicationService.TestUser]);

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DocumentExistsAsync(string document, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(User user) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAuthAccessApplicationService(IJwtTokenGenerator jwtTokenGenerator) : IAuthAccessApplicationService
    {
        internal static readonly User TestUser = CreateTestUser();

        /// <summary>
        /// Bumpa o TokenVersion do usuário fake — usado só pelo teste de revogação. Seguro
        /// para os outros testes do arquivo porque qualquer login novo gera o token já com o
        /// TokenVersion atual; só o cookie capturado ANTES da chamada fica obsoleto.
        /// </summary>
        internal static void RevokeTestUserSessions() => TestUser.RevokeSessions();

        public Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) =>
            Task.FromResult(BuildAuthResponse());

        public Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(BuildAuthResponse());

        public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private AuthResponse BuildAuthResponse()
        {
            var token = jwtTokenGenerator.Generate(TestUser, Guid.NewGuid());
            return new AuthResponse(TestUser.Id, TestUser.Name, TestUser.Email, TestUser.Role.ToString(), token.Token, "refresh-token-value", token.ExpiresAt);
        }

        private static User CreateTestUser()
        {
            var user = new User("Usuário Teste", "user@mail.com", "hash");
            user.SetRole(UserRole.Basic);
            return user;
        }
    }
}
