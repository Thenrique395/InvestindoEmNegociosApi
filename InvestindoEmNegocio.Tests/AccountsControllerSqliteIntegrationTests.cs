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
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AccountsControllerSqliteIntegrationTests
{
    [Fact]
    public async Task Create_List_And_Balance_Should_Persist_Data_Using_Sqlite()
    {
        await using var host = await TestAccountsApiHost.StartAsync(UserRole.Intermediate);
        var client = host.App.GetTestClient();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts")
        {
            Content = JsonContent.Create(new AccountRequest("Conta Integrada", AccountType.Checking, 2500m))
        };
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var createResponse = await client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Conta Integrada");
        created.InitialBalance.Should().Be(2500m);
        created.CurrentBalance.Should().Be(2500m);

        await host.SeedTransactionAsync(new AccountTransaction(
            created.Id,
            TestAccountsApiHost.TestUserId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            AccountTransactionKind.Credit,
            300m,
            "Aporte inicial"));

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts");
        listRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var listResponse = await client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        var accounts = await listResponse.Content.ReadFromJsonAsync<List<AccountResponse>>();
        accounts.Should().NotBeNull();
        accounts.Should().ContainSingle(x => x.Id == created.Id && x.CurrentBalance == 2800m);

        var balanceRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/accounts/{created.Id}/balance");
        balanceRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var balanceResponse = await client.SendAsync(balanceRequest);

        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balance = await balanceResponse.Content.ReadFromJsonAsync<AccountBalanceResponse>();
        balance.Should().NotBeNull();
        balance!.InitialBalance.Should().Be(2500m);
        balance.TransactionsNet.Should().Be(300m);
        balance.CurrentBalance.Should().Be(2800m);
    }

    [Fact]
    public async Task Create_Should_Return_Forbidden_For_Basic_Role()
    {
        await using var host = await TestAccountsApiHost.StartAsync(UserRole.Basic);
        var client = host.App.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts")
        {
            Content = JsonContent.Create(new AccountRequest("Account locked", AccountType.Checking, 100m))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Debt_And_NetWorth_Summaries_Should_Reflect_Open_Liabilities_And_Investments()
    {
        var positions = new List<InvestmentPositionDto>
        {
            new(
                Guid.NewGuid(),
                InvestmentType.ACOES,
                "PETR4",
                2m,
                150m,
                new DateOnly(2026, 1, 10),
                "Corretora",
                "Acoes",
                null,
                [],
                MarketPrice: 150m)
        };

        await using var host = await TestAccountsApiHost.StartAsync(UserRole.Intermediate, positions);
        var client = host.App.GetTestClient();

        Account? account = null;
        await host.SeedAsync(db =>
        {
            var spaceId = Guid.NewGuid();
            db.CardBrands.Add(new CardBrand(1, "Visa", "visa"));

            account = new Account(TestAccountsApiHost.TestUserId, spaceId, "Conta resumo", AccountType.Checking, 1000m);
            db.Accounts.Add(account);
            db.AccountTransactions.Add(new AccountTransaction(account.Id, TestAccountsApiHost.TestUserId, spaceId, DateTime.UtcNow, AccountTransactionKind.Credit, 200m, "Saldo extra"));

            var card = new Card(TestAccountsApiHost.TestUserId, spaceId, 1, "Henrique Santos", "Visa resumo", "1234", "Banco X", 5000m, 10, 18);
            db.Cards.Add(card);

            var cardPlan = new MoneyPlan(
                TestAccountsApiHost.TestUserId,
                spaceId,
                MoneyType.Expense,
                "Mercado cartao",
                500m,
                ScheduleType.OneTime,
                new DateOnly(2026, 3, 5),
                cardId: card.Id);
            var otherPlan = new MoneyPlan(
                TestAccountsApiHost.TestUserId,
                spaceId,
                MoneyType.Expense,
                "Seguro anual",
                400m,
                ScheduleType.OneTime,
                new DateOnly(2026, 3, 8));
            db.MoneyPlans.AddRange(cardPlan, otherPlan);

            var cardInstallment = new MoneyInstallment(
                cardPlan.Id,
                TestAccountsApiHost.TestUserId,
                spaceId,
                1,
                new DateOnly(2026, 3, 18),
                500m,
                statementYear: 2026,
                statementMonth: 3,
                statementCloseDate: new DateOnly(2026, 3, 10),
                statementDueDate: new DateOnly(2026, 3, 18));
            var otherInstallment = new MoneyInstallment(
                otherPlan.Id,
                TestAccountsApiHost.TestUserId,
                spaceId,
                1,
                new DateOnly(2026, 3, 25),
                400m);
            db.MoneyInstallments.AddRange(cardInstallment, otherInstallment);
            db.MoneyPayments.Add(new MoneyPayment(cardInstallment.Id, TestAccountsApiHost.TestUserId, spaceId, DateTime.UtcNow, 200m));

            return Task.CompletedTask;
        });

        var debtRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts/summary/debts?referenceDate=2026-03-15");
        debtRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var debtResponse = await client.SendAsync(debtRequest);

        debtResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var debt = await debtResponse.Content.ReadFromJsonAsync<DebtSummaryResponse>();
        debt.Should().NotBeNull();
        debt!.TotalDebt.Should().Be(700m);
        debt.CardDebt.Should().Be(300m);
        debt.OtherDebt.Should().Be(400m);
        debt.OpenItemsCount.Should().Be(2);

        var netWorthRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts/summary/net-worth?referenceDate=2026-03-15");
        netWorthRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var netWorthResponse = await client.SendAsync(netWorthRequest);

        netWorthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var netWorth = await netWorthResponse.Content.ReadFromJsonAsync<NetWorthSummaryResponse>();
        netWorth.Should().NotBeNull();
        netWorth!.Assets.AccountsBalance.Should().Be(1200m);
        netWorth.Assets.InvestmentsBalance.Should().Be(300m);
        netWorth.Liabilities.CardDebt.Should().Be(300m);
        netWorth.Liabilities.OtherOpenLiabilities.Should().Be(400m);
        netWorth.NetWorth.Should().Be(800m);
    }

    [Fact]
    public async Task Delete_Should_SoftDelete_Account_And_Cascade_To_Transactions_And_Allow_Recreating_Same_Name()
    {
        await using var host = await TestAccountsApiHost.StartAsync(UserRole.Intermediate);
        var client = host.App.GetTestClient();

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts")
        {
            Content = JsonContent.Create(new AccountRequest("Conta Reaproveitada", AccountType.Checking, 100m))
        };
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        await host.SeedTransactionAsync(new AccountTransaction(
            created!.Id, TestAccountsApiHost.TestUserId, Guid.NewGuid(), DateTime.UtcNow, AccountTransactionKind.Credit, 50m, "Aporte"));

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/accounts/{created.Id}");
        deleteRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Índice único parcial: deve ser possível recriar uma conta com o mesmo nome
        // depois da exclusão (a linha excluída não pode mais ocupar o slot do índice).
        var recreateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts")
        {
            Content = JsonContent.Create(new AccountRequest("Conta Reaproveitada", AccountType.Checking, 200m))
        };
        recreateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var recreateResponse = await client.SendAsync(recreateRequest);
        recreateResponse.StatusCode.Should().Be(HttpStatusCode.Created, await recreateResponse.Content.ReadAsStringAsync());

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts");
        listRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var listResponse = await client.SendAsync(listRequest);
        var accounts = await listResponse.Content.ReadFromJsonAsync<List<AccountResponse>>();
        accounts!.Should().ContainSingle(x => x.Name == "Conta Reaproveitada" && x.InitialBalance == 200m);

        await host.SeedAsync(async db =>
        {
            var deletedAccount = await db.Accounts.IgnoreQueryFilters().SingleAsync(x => x.Id == created.Id);
            deletedAccount.DeletedAt.Should().NotBeNull();

            var deletedTransaction = await db.AccountTransactions.IgnoreQueryFilters().SingleAsync(x => x.AccountId == created.Id);
            deletedTransaction.DeletedAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task AccountEndpoints_Should_Not_Expose_Other_Users_Account_Idor()
    {
        await using var host = await TestAccountsApiHost.StartAsync(UserRole.Intermediate);
        var client = host.App.GetTestClient();

        // Conta pertencente a OUTRO usuário (não o autenticado TestUserId).
        var otherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherAccountId = Guid.Empty;
        await host.SeedAsync(async db =>
        {
            var account = new Account(otherUserId, Guid.NewGuid(), "Conta do outro usuário", AccountType.Checking, 5000m);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            otherAccountId = account.Id;
        });

        // Autenticado como TestUserId, tentar LER o saldo da conta alheia → 404.
        var balanceRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/accounts/{otherAccountId}/balance");
        balanceRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var balanceResponse = await client.SendAsync(balanceRequest);
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // ...e tentar EXCLUIR a conta alheia → 404.
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/accounts/{otherAccountId}");
        deleteRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A conta do outro usuário permanece intacta (não foi lida nem removida).
        await host.SeedAsync(async db =>
        {
            var stillExists = await db.Accounts.IgnoreQueryFilters().AnyAsync(x => x.Id == otherAccountId);
            stillExists.Should().BeTrue();
        });
    }

    private sealed class TestAccountsApiHost(WebApplication app, SqliteConnection connection) : IAsyncDisposable
    {
        internal static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public WebApplication App { get; } = app;

        public static async Task<TestAccountsApiHost> StartAsync(UserRole role, List<InvestmentPositionDto>? positions = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
            builder.Services.AddDbContext<InvestDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<IInvestDbContext>(sp => sp.GetRequiredService<InvestDbContext>());
            builder.Services.AddSingleton<ICurrentSpaceAccessor>(Mock.Of<ICurrentSpaceAccessor>());

            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<IAccountTransactionRepository, AccountTransactionRepository>();
            builder.Services.AddScoped<IMoneyInstallmentRepository, MoneyInstallmentRepository>();
            builder.Services.AddScoped<IMoneyPaymentRepository, MoneyPaymentRepository>();
            builder.Services.AddScoped<IMoneyPlanRepository, MoneyPlanRepository>();
            builder.Services.AddScoped<ICardRepository, CardRepository>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ILoanContractRepository, LoanContractRepository>();
            builder.Services.AddScoped<ILoanInstallmentRepository, LoanInstallmentRepository>();
            builder.Services.AddScoped<IAccountQueryService, AccountQueryService>();
            builder.Services.AddScoped<IAccountCommandService, AccountCommandService>();
            builder.Services.AddScoped<IAccountTransactionQueryService, AccountTransactionQueryService>();
            builder.Services.AddScoped<IAccountTransferService, AccountTransferService>();
            builder.Services.AddScoped<IAccountsService, AccountsService>();
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<IAccountAnalyticsService, AccountAnalyticsService>();
            builder.Services.AddSingleton<IOfxImportService, NoOpOfxImportService>();
            builder.Services.AddSingleton<ICsvImportService, NoOpCsvImportService>();
            var investmentsService = new StubInvestmentsService(positions ?? []);
            builder.Services.AddSingleton<IInvestmentsService>(investmentsService);
            builder.Services.AddSingleton<IInvestmentPositionQueryService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<IInvestmentMarketEnrichmentService>(sp => sp.GetRequiredService<IInvestmentsService>());
            builder.Services.AddSingleton<ICashflowProjectionEngine, StubCashflowProjectionEngine>();
            builder.Services.AddSingleton<IRiskBotService, StubRiskBotService>();
            builder.Services.AddSingleton<IInsightEngineService, StubInsightEngineService>();
            builder.Services.AddSingleton<IRecommendationEngineService, StubRecommendationEngineService>();

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestAuth";
                    options.DefaultChallengeScheme = "TestAuth";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options =>
                {
                    options.ClaimsIssuer = role.ToString();
                });

            builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder("TestAuth")
                    .RequireAuthenticatedUser()
                    .Build());
            builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);

            var app = builder.Build();
            app.UseGlobalProblemDetails(includeExceptionDetails: true);
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            await app.StartAsync();
            return new TestAccountsApiHost(app, connection);
        }

        public async Task SeedTransactionAsync(AccountTransaction transaction)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
            db.AccountTransactions.Add(transaction);
            await db.SaveChangesAsync();
        }

        public async Task SeedAsync(Func<InvestDbContext, Task> seed)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
            await seed(db);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            await connection.DisposeAsync();
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
            var role = Options.ClaimsIssuer ?? UserRole.Intermediate.ToString();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestAccountsApiHost.TestUserId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class NoOpOfxImportService : IOfxImportService
    {
        public Task<OfxExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken)
            => throw new NotSupportedException("OFX não é exercitado neste teste.");

        public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("OFX não é exercitado neste teste.");
    }

    private sealed class NoOpCsvImportService : ICsvImportService
    {
        public Task<CsvExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken)
            => throw new NotSupportedException("CSV não é exercitado neste teste.");

        public Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("CSV não é exercitado neste teste.");
    }

    private sealed class StubInvestmentsService(List<InvestmentPositionDto> positions) : IInvestmentsService
    {
        public Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<InvestmentGoalDto?>(null);
        public Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(positions.ToList());
        public Task<InvestmentPositionDto?> GetPositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<InvestmentPositionDto?>(null);
        public Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid id, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeletePositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId, CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InvestmentPositionDto>> EnrichWithMarketAsync(List<InvestmentPositionDto> items, CancellationToken cancellationToken = default) => Task.FromResult(items);
    }

    private sealed class StubCashflowProjectionEngine : ICashflowProjectionEngine
    {
        public Task<CashflowProjectionResponse> ProjectAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new CashflowProjectionResponse(period, referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), 0m, 0m, 0m, null, null, []));
    }

    private sealed class StubRiskBotService : IRiskBotService
    {
        public Task<RiskBotAssessmentResponse> AssessAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new RiskBotAssessmentResponse(period, referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow), 0, "safe", "low", null, 0m, 0m, 0m, [], [], []));
    }

    private sealed class StubInsightEngineService : IInsightEngineService
    {
        public Task<InsightEngineResponse> BuildAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new InsightEngineResponse(period, referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow), null, []));
    }

    private sealed class StubRecommendationEngineService : IRecommendationEngineService
    {
        public Task<RecommendationEngineResponse> BuildAsync(Guid userId, string period = "month", DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new RecommendationEngineResponse(period, referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow), 0, []));
    }
}
