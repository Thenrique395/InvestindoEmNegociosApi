using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using FluentValidation.AspNetCore;
using InvestindoEmNegocio.Application.Validation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class ApiErrorContractIntegrationTests
{
    [Fact]
    public async Task Auth_Login_Should_Return_ProblemDetails_When_Application_Service_Throws_AppProblemException()
    {
        var fakeAuthApplicationService = new FakeAuthApplicationService
        {
            OnLoginAsync = (_, _, _, _) => Task.FromException<AuthResponse>(
                new AppProblemException("Credenciais inválidas", "Email ou senha incorretos.", StatusCodes.Status401Unauthorized))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
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
        var fakeDataPortabilityApplicationService = new FakeDataPortabilityApplicationService
        {
            OnExportAsync = (_, _) => Task.FromException<(string FileName, byte[] Content)>(
                new AppProblemException(
                    "Funcionalidade desabilitada",
                    "A exportação e importação de dados está desabilitada.",
                    StatusCodes.Status404NotFound))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(fakeDataPortabilityApplicationService);
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/data-portability/export");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Funcionalidade desabilitada", problem!.Title);
        Assert.Equal("/api/v1/data-portability/export", problem.Instance);
    }

    [Fact]
    public async Task Auth_Register_Should_Return_ProblemDetails_When_Application_Service_Throws_Conflict()
    {
        var fakeAuthApplicationService = new FakeAuthApplicationService
        {
            OnRegisterAsync = (_, _) => Task.FromException<AuthResponse>(
                new AppProblemException("Email já existe", "Já existe uma conta para este email.", StatusCodes.Status409Conflict))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
        });

        var response = await app.GetTestClient().PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest("User", "user@mail.com", "Password123!"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Email já existe", problem!.Title);
        Assert.Equal("/api/v1/auth/register", problem.Instance);
    }

    [Fact]
    public async Task Auth_Login_Should_Return_500_ProblemDetails_When_Application_Service_Throws_Unexpected_Exception()
    {
        var fakeAuthApplicationService = new FakeAuthApplicationService
        {
            OnLoginAsync = (_, _, _, _) => Task.FromException<AuthResponse>(new InvalidOperationException("boom"))
        };

        await using var app = await BuildTestAppAsync(services =>
        {
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
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
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
        });

        using var form = new MultipartFormDataContent();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/data-portability/import");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = form;

        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Dados inválidos.", problem!.Title);
        Assert.Equal("Revise os campos informados.", problem.Detail);
    }

    [Fact]
    public async Task Accounts_Create_Should_Return_400_ValidationProblemDetails_When_Request_Is_Invalid()
    {
        await using var app = await BuildTestAppAsync(services =>
        {
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
            var accounts = new FakeAccountsService();
            services.AddSingleton<IAccountsService>(accounts);
            services.AddSingleton<IAccountQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountCommandService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountTransactionQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountTransferService>(sp => sp.GetRequiredService<IAccountsService>());
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = JsonContent.Create(new AccountRequest(string.Empty, AccountType.Checking, -10m));
        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Dados inválidos.", problem!.Title);
        Assert.Contains("Name", problem.Errors.Keys);
        Assert.Contains("InitialBalance", problem.Errors.Keys);
    }

    [Fact]
    public async Task Accounts_Create_Should_Return_400_ProblemDetails_When_Service_Throws_ArgumentException()
    {
        await using var app = await BuildTestAppAsync(services =>
        {
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
            var accounts = new FakeAccountsService
            {
                OnCreateAsync = (_, _, _) => Task.FromException<AccountResponse>(new ArgumentException("Já existe uma conta com esse nome."))
            };
            services.AddSingleton<IAccountsService>(accounts);
            services.AddSingleton<IAccountQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountCommandService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountTransactionQueryService>(sp => sp.GetRequiredService<IAccountsService>());
            services.AddSingleton<IAccountTransferService>(sp => sp.GetRequiredService<IAccountsService>());
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = JsonContent.Create(new AccountRequest("Conta principal", AccountType.Checking, 0m));
        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Conta inválida", problem!.Title);
        Assert.Equal("Já existe uma conta com esse nome.", problem.Detail);
    }

    [Fact]
    public async Task Categories_Create_Should_Return_409_ProblemDetails_When_Service_Throws_InvalidOperationException()
    {
        await using var app = await BuildTestAppAsync(services =>
        {
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
            services.AddSingleton<ICategoriesService>(new FakeCategoriesService
            {
                OnCreateAsync = (_, _, _) => Task.FromException<CategoryResponse>(new InvalidOperationException("Categoria já existe para o usuário."))
            });
            services.AddSingleton<IAuditService>(new FakeAuditService());
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        request.Content = JsonContent.Create(new UpsertCategoryRequest("Moradia", MoneyType.Expense));
        var response = await app.GetTestClient().SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Conflito de categoria", problem!.Title);
        Assert.Equal("Categoria já existe para o usuário.", problem.Detail);
    }

    [Fact]
    public async Task AccountImports_ImportOfx_Should_Return_422_ProblemDetails_When_Service_Throws_InvalidOperationException()
    {
        await using var app = await BuildTestAppAsync(services =>
        {
            var fakeAuthApplicationService = new FakeAuthApplicationService();
            services.AddSingleton<IAuthAccessApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthRegistrationApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IAuthPasswordApplicationService>(fakeAuthApplicationService);
            services.AddSingleton<IDataPortabilityApplicationService>(new FakeDataPortabilityApplicationService());
            services.AddSingleton<IOfxImportService>(new FakeOfxImportService
            {
                OnImportAsync = (_, _, _) => Task.FromException<BankStatementImportResultResponse>(new InvalidOperationException("Importação OFX rejeitada."))
            });
            services.AddSingleton<ICsvImportService>(new FakeCsvImportService());
        });

        var request = new BankStatementImportRequest(
            Guid.NewGuid(),
            true,
            [
                new BankStatementImportItemDto(
                    "2026-01-10",
                    100m,
                    AccountTransactionKind.Credit,
                    "Salário",
                    null,
                    null,
                    null)
            ]);
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/ofx/import");
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        message.Content = JsonContent.Create(request);

        var response = await app.GetTestClient().SendAsync(message);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Rejected OFX import", problem!.Title);
        Assert.Equal("Importação OFX rejeitada.", problem.Detail);
    }

    private static async Task<WebApplication> BuildTestAppAsync(Action<IServiceCollection> registerFakes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Dados inválidos.",
                    Detail = "Revise os campos informados.",
                    Instance = context.HttpContext.Request.Path
                };
                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserRequestValidator>();
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
                new Claim(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakeAuthApplicationService : IAuthAccessApplicationService, IAuthRegistrationApplicationService, IAuthPasswordApplicationService
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

    private sealed class FakeDataPortabilityApplicationService : IDataPortabilityApplicationService
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

    private sealed class FakeAccountsService : IAccountsService
    {
        public Func<Guid, AccountRequest, CancellationToken, Task<AccountResponse>>? OnCreateAsync { get; init; }

        public Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountResponse>>([]);

        public Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default) =>
            OnCreateAsync?.Invoke(userId, request, cancellationToken)
            ?? Task.FromResult(new AccountResponse(Guid.NewGuid(), request.Name, request.Type, request.InitialBalance, request.InitialBalance, request.IsActive, DateTime.UtcNow, DateTime.UtcNow));

        public Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountResponse?>(null);

        public Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountBalanceResponse?>(null);

        public Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(Guid userId, Guid accountId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountTransactionResponse>?>([]);

        public Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountTransferResponse?>(null);
    }

    private sealed class FakeCategoriesService : ICategoriesService
    {
        public Func<Guid, UpsertCategoryRequest, CancellationToken, Task<CategoryResponse>>? OnCreateAsync { get; init; }

        public Task<IReadOnlyList<CategoryResponse>> ListAsync(Guid userId, MoneyType? appliesTo, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryResponse>>([]);

        public Task<CategoryResponse> CreateAsync(Guid userId, UpsertCategoryRequest request, CancellationToken cancellationToken = default) =>
            OnCreateAsync?.Invoke(userId, request, cancellationToken)
            ?? Task.FromResult(new CategoryResponse(Guid.NewGuid(), request.Name, request.AppliesTo, false));

        public Task<CategoryResponse?> UpdateAsync(Guid userId, Guid id, UpsertCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<CategoryResponse?>(null);

        public Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeOfxImportService : IOfxImportService
    {
        public Func<Guid, Guid?, Stream, CancellationToken, Task<OfxExtractResponse>>? OnExtractAsync { get; init; }
        public Func<Guid, BankStatementImportRequest, CancellationToken, Task<BankStatementImportResultResponse>>? OnImportAsync { get; init; }

        public Task<OfxExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken) =>
            OnExtractAsync?.Invoke(userId, accountId, stream, cancellationToken)
            ?? Task.FromResult(new OfxExtractResponse(null, null, null, null, null, null, null, [], string.Empty));

        public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken) =>
            OnImportAsync?.Invoke(userId, request, cancellationToken)
            ?? Task.FromResult(new BankStatementImportResultResponse(0, 0));
    }

    private sealed class FakeCsvImportService : ICsvImportService
    {
        public Task<CsvExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken) =>
            Task.FromResult(new CsvExtractResponse(",", [], [], string.Empty));

        public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BankStatementImportResultResponse(0, 0));
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task LogAsync(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
