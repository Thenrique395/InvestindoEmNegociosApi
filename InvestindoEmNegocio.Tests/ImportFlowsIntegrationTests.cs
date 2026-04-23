using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using InvestindoEmNegocio.Infrastructure.Api;
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

namespace InvestindoEmNegocio.Tests;

public class ImportFlowsIntegrationTests
{
    [Fact]
    public async Task Ofx_Extract_And_Import_Should_Create_Transactions_And_Dedupe()
    {
        await using var host = await TestImportApiHost.StartAsync();
        var client = host.App.GetTestClient();
        var accountId = await host.CreateAccountAsync("Conta OFX");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(accountId.ToString()), "accountId");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(SampleOfx)), "file", "sample.ofx");

        var extractRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/ofx/extract");
        extractRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        extractRequest.Content = form;

        var extractResponse = await client.SendAsync(extractRequest);
        extractResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await extractResponse.Content.ReadFromJsonAsync<OfxExtractResponse>();
        preview.Should().NotBeNull();
        preview!.BankId.Should().Be("341");
        preview.Items.Should().HaveCount(2);
        preview.Items.Should().OnlyContain(x => !x.IsDuplicate);

        var importRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/ofx/import")
        {
            Content = JsonContent.Create(new BankStatementImportRequest(accountId, false, preview.Items
                .Select(x => new BankStatementImportItemDto(x.PostedAt, x.Amount, x.Kind, x.Description, x.Memo, x.ExternalId, x.Type))
                .ToList()))
        };
        importRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var importResponse = await client.SendAsync(importRequest);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var importResult = await importResponse.Content.ReadFromJsonAsync<BankStatementImportResultResponse>();
        importResult.Should().NotBeNull();
        importResult!.Created.Should().Be(2);
        importResult.Skipped.Should().Be(0);

        var dedupeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/ofx/import")
        {
            Content = JsonContent.Create(new BankStatementImportRequest(accountId, true, preview.Items
                .Select(x => new BankStatementImportItemDto(x.PostedAt, x.Amount, x.Kind, x.Description, x.Memo, x.ExternalId, x.Type))
                .ToList()))
        };
        dedupeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var dedupeResponse = await client.SendAsync(dedupeRequest);
        dedupeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dedupeResult = await dedupeResponse.Content.ReadFromJsonAsync<BankStatementImportResultResponse>();
        dedupeResult.Should().NotBeNull();
        dedupeResult!.Created.Should().Be(0);
        dedupeResult.Skipped.Should().Be(2);

        var transactions = await GetTransactionsAsync(client, accountId);
        transactions.Should().HaveCount(2);
        transactions.Should().Contain(x => x.Description == "PIX SALARIO" && x.SourceType == "BankStatementImport");
    }

    [Fact]
    public async Task Csv_Extract_And_Import_Should_Create_Transactions()
    {
        await using var host = await TestImportApiHost.StartAsync();
        var client = host.App.GetTestClient();
        var accountId = await host.CreateAccountAsync("Conta CSV");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(accountId.ToString()), "accountId");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(SampleCsv)), "file", "sample.csv");

        var extractRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/csv/extract");
        extractRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        extractRequest.Content = form;

        var extractResponse = await client.SendAsync(extractRequest);
        extractResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await extractResponse.Content.ReadFromJsonAsync<CsvExtractResponse>();
        preview.Should().NotBeNull();
        preview!.DetectedColumns.Should().Contain("data");
        preview.Items.Should().HaveCount(2);

        var importRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/csv/import")
        {
            Content = JsonContent.Create(new BankStatementImportRequest(accountId, true, preview.Items
                .Select(x => new BankStatementImportItemDto(x.PostedAt, x.Amount, x.Kind, x.Description, x.Memo, x.ExternalId, x.Type))
                .ToList()))
        };
        importRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var importResponse = await client.SendAsync(importRequest);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var importResult = await importResponse.Content.ReadFromJsonAsync<BankStatementImportResultResponse>();
        importResult.Should().NotBeNull();
        importResult!.Created.Should().Be(2);

        var transactions = await GetTransactionsAsync(client, accountId);
        transactions.Should().HaveCount(2);
        transactions.Should().Contain(x => x.Description == "Mercado bairro");
    }

    [Fact]
    public async Task Invoice_Import_Should_Create_Statement_Item_And_Skip_Duplicates()
    {
        await using var host = await TestImportApiHost.StartAsync();
        var client = host.App.GetTestClient();
        var cardId = await host.CreateCardAsync("Cartao fatura");

        var request = new InvoiceImportRequest(
            cardId,
            CategoryId: null,
            DefaultDueDate: "18/03/2026",
            StatementCloseDate: null,
            InvoiceTotal: null,
            ImportIdempotencyKey: null,
            SkipDuplicates: true,
            Items:
            [
                new InvoiceImportItemRequest(
                    Date: "05/03/2026",
                    Description: "Mercado Fatura",
                    Amount: "300,00",
                    BaseDescription: "Mercado Fatura")
            ]);

        var importResponse = await PostAuthorizedJsonAsync(client, "/api/v1/invoice-import/import", request);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var importResult = await importResponse.Content.ReadFromJsonAsync<InvoiceImportResultResponse>();
        importResult.Should().NotBeNull();
        importResult!.Created.Should().Be(1);
        importResult.Skipped.Should().Be(0);
        importResult.Failed.Should().Be(0);

        var duplicateResponse = await PostAuthorizedJsonAsync(client, "/api/v1/invoice-import/import", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicateResult = await duplicateResponse.Content.ReadFromJsonAsync<InvoiceImportResultResponse>();
        duplicateResult.Should().NotBeNull();
        duplicateResult!.Created.Should().Be(0);
        duplicateResult.Skipped.Should().Be(1);

        var statementsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cards/{cardId}/statements?year=2026&month=3");
        statementsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var statementsResponse = await client.SendAsync(statementsRequest);

        statementsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statements = await statementsResponse.Content.ReadFromJsonAsync<List<CardStatementCycleResponse>>();
        statements.Should().NotBeNull();
        statements.Should().ContainSingle();
        statements![0].Items.Should().ContainSingle(x => x.Title == "Mercado Fatura" && x.Amount == 300m);

        var debtRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cards/debt/total");
        debtRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var debtResponse = await client.SendAsync(debtRequest);
        debtResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var debt = await debtResponse.Content.ReadFromJsonAsync<TotalDebtEnvelope>();
        debt.Should().NotBeNull();
        debt!.Total.Should().Be(300m);
    }

    private static async Task<HttpResponseMessage> PostAuthorizedJsonAsync<T>(HttpClient client, string url, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        return await client.SendAsync(request);
    }

    private static async Task<List<AccountTransactionResponse>> GetTransactionsAsync(HttpClient client, Guid accountId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/accounts/{accountId}/transactions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<AccountTransactionResponse>>();
        items.Should().NotBeNull();
        return items!;
    }

    private sealed record TotalDebtEnvelope(decimal Total);

    private sealed class TestImportApiHost(WebApplication app, SqliteConnection connection) : IAsyncDisposable
    {
        internal static readonly Guid TestUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public WebApplication App { get; } = app;

        public static async Task<TestImportApiHost> StartAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
            builder.Services.AddDbContext<InvestDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<IInvestDbContext>(sp => sp.GetRequiredService<InvestDbContext>());

            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<IAccountTransactionRepository, AccountTransactionRepository>();
            builder.Services.AddScoped<IMoneyInstallmentRepository, MoneyInstallmentRepository>();
            builder.Services.AddScoped<IMoneyPaymentRepository, MoneyPaymentRepository>();
            builder.Services.AddScoped<IMoneyPlanRepository, MoneyPlanRepository>();
            builder.Services.AddScoped<ICardRepository, CardRepository>();
            builder.Services.AddScoped<ICardBrandRepository, CardBrandRepository>();
            builder.Services.AddScoped<ILoanContractRepository, LoanContractRepository>();
            builder.Services.AddScoped<ILoanInstallmentRepository, LoanInstallmentRepository>();

            builder.Services.AddScoped<IAccountsService, AccountsService>();
            builder.Services.AddScoped<IAccountAnalyticsService, AccountAnalyticsService>();
            builder.Services.AddScoped<ICardsService, CardsService>();
            builder.Services.AddScoped<IPlansService, PlansService>();
            builder.Services.AddScoped<IBankStatementImportEngine, BankStatementImportEngine>();
            builder.Services.AddScoped<IOfxImportService, OfxImportService>();
            builder.Services.AddScoped<ICsvImportService, CsvImportService>();
            builder.Services.AddScoped<IInvoiceImportService, InvoiceImportService>();
            builder.Services.AddScoped<IImportIdentityEngine, ImportIdentityEngine>();
            builder.Services.AddSingleton<InvoiceParserFactory>();

            builder.Services.AddSingleton<ICategorizationService, NoOpCategorizationService>();
            builder.Services.AddSingleton<IRecurrenceDetectorService, NoOpRecurrenceDetectorService>();
            builder.Services.AddSingleton<IAuditService, NoOpAuditService>();
            builder.Services.AddSingleton<IInvestmentsService, StubInvestmentsService>();
            builder.Services.AddSingleton<ICashflowProjectionEngine, StubCashflowProjectionEngine>();
            builder.Services.AddSingleton<IRiskBotService, StubRiskBotService>();
            builder.Services.AddSingleton<IInsightEngineService, StubInsightEngineService>();
            builder.Services.AddSingleton<IRecommendationEngineService, StubRecommendationEngineService>();

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
            return new TestImportApiHost(app, connection);
        }

        public async Task<Guid> CreateAccountAsync(string name)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
            var account = new Account(TestUserId, name, AccountType.Checking, 1000m);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            return account.Id;
        }

        public async Task<Guid> CreateCardAsync(string nickname)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
            db.CardBrands.Add(new CardBrand(1, "Visa", "visa"));
            var card = new Card(TestUserId, 1, "Henrique Santos", nickname, "1234", "Banco X", 5000m, 10, 18);
            db.Cards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
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
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestImportApiHost.TestUserId.ToString()),
                new Claim(ClaimTypes.Role, UserRole.Intermediate.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class NoOpCategorizationService : ICategorizationService
    {
        public Task<CategorizationSuggestionDto?> SuggestAsync(Guid userId, MoneyType type, string description, CancellationToken cancellationToken = default)
            => Task.FromResult<CategorizationSuggestionDto?>(null);

        public Task LearnAsync(Guid userId, MoneyType type, string description, Guid categoryId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpRecurrenceDetectorService : IRecurrenceDetectorService
    {
        public Task<RecurrenceSuggestionDto?> SuggestAsync(Guid userId, MoneyType type, string description, decimal amount, DateTime? occurredAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<RecurrenceSuggestionDto?>(null);
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubInvestmentsService : IInvestmentsService
    {
        public Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<InvestmentGoalDto?>(null);
        public Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<InvestmentPositionDto>());
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

    private const string SampleCsv = """
data;descricao;valor;id
05/03/2026;Mercado bairro;-120,50;csv-001
06/03/2026;Pix cliente;950,00;csv-002
""";

    private const string SampleOfx = """
OFXHEADER:100
DATA:OFXSGML
VERSION:102
SECURITY:NONE
ENCODING:USASCII
CHARSET:1252
COMPRESSION:NONE
OLDFILEUID:NONE
NEWFILEUID:NONE

<OFX>
  <BANKMSGSRSV1>
    <STMTTRNRS>
      <STMTRS>
        <BANKACCTFROM>
          <BANKID>341
          <ACCTID>123456
          <ACCTTYPE>CHECKING
        </BANKACCTFROM>
        <BANKTRANLIST>
          <DTSTART>20260301000000
          <DTEND>20260331235959
          <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20260305120000
            <TRNAMT>-120.50
            <FITID>ofx-001
            <NAME>MERCADO CENTRAL
            <MEMO>COMPRA NO CARTAO
          </STMTTRN>
          <STMTTRN>
            <TRNTYPE>CREDIT
            <DTPOSTED>20260306150000
            <TRNAMT>950.00
            <FITID>ofx-002
            <NAME>PIX SALARIO
            <MEMO>CREDITO EMPRESA
          </STMTTRN>
        </BANKTRANLIST>
        <LEDGERBAL>
          <BALAMT>1829.50
        </LEDGERBAL>
      </STMTRS>
    </STMTTRNRS>
  </BANKMSGSRSV1>
</OFX>
""";
}
