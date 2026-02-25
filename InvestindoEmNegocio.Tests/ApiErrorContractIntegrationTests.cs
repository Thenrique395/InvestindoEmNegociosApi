using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class ApiErrorContractIntegrationTests
{
    [Fact]
    public async Task Auth_Login_Should_Return_ProblemDetails_When_Facade_Throws_AppProblemException()
    {
        var fakeAuthFacade = new FakeAuthFacadeService
        {
            OnLoginAsync = (_, _, _, _) => Task.FromException<AuthResponse>(
                new AppProblemException("Credenciais inválidas", "E-mail ou senha incorretos.", StatusCodes.Status401Unauthorized))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthFacadeService>(fakeAuthFacade);
            services.AddSingleton<IDataPortabilityFacadeService>(new FakeDataPortabilityFacadeService());
        });

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "wrong"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Credenciais inválidas", problem!.Title);
        Assert.Equal("/api/v1/auth/login", problem.Instance);
    }

    [Fact]
    public async Task DataPortability_Export_Should_Return_ProblemDetails_When_Feature_Is_Disabled()
    {
        var fakeDataPortabilityFacade = new FakeDataPortabilityFacadeService
        {
            OnExportAsync = (_, _) => Task.FromException<(string FileName, byte[] Content)>(
                new AppProblemException(
                    "Funcionalidade desabilitada",
                    "A exportação/importação de dados está desativada.",
                    StatusCodes.Status404NotFound))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthFacadeService>(new FakeAuthFacadeService());
            services.AddSingleton<IDataPortabilityFacadeService>(fakeDataPortabilityFacade);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dataportability/export");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Funcionalidade desabilitada", problem!.Title);
        Assert.Equal("/api/v1/dataportability/export", problem.Instance);
    }

    [Fact]
    public async Task Auth_Register_Should_Return_ProblemDetails_When_Facade_Throws_Conflict()
    {
        var fakeAuthFacade = new FakeAuthFacadeService
        {
            OnRegisterAsync = (_, _) => Task.FromException<AuthResponse>(
                new AppProblemException("E-mail já existe", "Já existe cadastro para este e-mail.", StatusCodes.Status409Conflict))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthFacadeService>(fakeAuthFacade);
            services.AddSingleton<IDataPortabilityFacadeService>(new FakeDataPortabilityFacadeService());
        });

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("User", "user@mail.com", "Password123!"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("E-mail já existe", problem!.Title);
        Assert.Equal("/api/v1/auth/register", problem.Instance);
    }

    [Fact]
    public async Task Auth_Login_Should_Return_500_ProblemDetails_When_Facade_Throws_Unexpected_Exception()
    {
        var fakeAuthFacade = new FakeAuthFacadeService
        {
            OnLoginAsync = (_, _, _, _) => Task.FromException<AuthResponse>(new InvalidOperationException("boom"))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthFacadeService>(fakeAuthFacade);
            services.AddSingleton<IDataPortabilityFacadeService>(new FakeDataPortabilityFacadeService());
        });

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("user@mail.com", "pwd"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Erro interno do servidor.", problem!.Title);
        Assert.Equal("/api/v1/auth/login", problem.Instance);
        Assert.Null(problem.Detail);
    }

    [Fact]
    public async Task DataPortability_Import_Should_Return_400_ProblemDetails_When_File_Is_Missing()
    {
        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthFacadeService>(new FakeAuthFacadeService());
            services.AddSingleton<IDataPortabilityFacadeService>(new FakeDataPortabilityFacadeService());
        });

        using var form = new MultipartFormDataContent();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dataportability/import");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = form;

        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("One or more validation errors occurred.", problem!.Title);
    }

    private static async Task<WebApplication> BuildTestAppAsync(Action<IServiceCollection> registerFakes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", opt =>
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
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", _ => { });
        builder.Services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder("TestAuth")
                .RequireAuthenticatedUser()
                .Build());
        builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);

        registerFakes(builder.Services);

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

    private sealed class FakeAuthFacadeService : IAuthFacadeService
    {
        public Func<RegisterUserRequest, CancellationToken, Task<AuthResponse>>? OnRegisterAsync { get; init; }
        public Func<LoginRequest, string?, string?, CancellationToken, Task<AuthResponse>>? OnLoginAsync { get; init; }

        public Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            OnRegisterAsync?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new AuthResponse(Guid.NewGuid(), "User", "user@mail.com", "User", "token", "refresh", DateTime.UtcNow.AddHours(1)));

        public Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) =>
            OnLoginAsync?.Invoke(request, ipAddress, userAgent, cancellationToken)
            ?? Task.FromResult(new AuthResponse(Guid.NewGuid(), "User", "user@mail.com", "User", "token", "refresh", DateTime.UtcNow.AddHours(1)));

        public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthResponse(Guid.NewGuid(), "User", "user@mail.com", "User", "token", "refresh", DateTime.UtcNow.AddHours(1)));

        public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDataPortabilityFacadeService : IDataPortabilityFacadeService
    {
        public Func<Guid, CancellationToken, Task<(string FileName, byte[] Content)>>? OnExportAsync { get; init; }
        public Func<Guid, Stream, long, bool, CancellationToken, Task<ImportUserDataResult>>? OnImportAsync { get; init; }

        public Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default) =>
            OnExportAsync?.Invoke(userId, cancellationToken)
            ?? Task.FromResult<(string FileName, byte[] Content)>(("export.json", "{}"u8.ToArray()));

        public Task<ImportUserDataResult> ImportAsync(
            Guid userId,
            Stream stream,
            long fileLength,
            bool replaceExisting,
            CancellationToken cancellationToken = default) =>
            OnImportAsync?.Invoke(userId, stream, fileLength, replaceExisting, cancellationToken)
            ?? Task.FromResult(new ImportUserDataResult(0));
    }
}
