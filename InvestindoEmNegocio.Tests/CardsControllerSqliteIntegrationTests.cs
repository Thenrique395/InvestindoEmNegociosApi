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

public class CardsControllerSqliteIntegrationTests
{
    [Fact]
    public async Task Create_List_Debt_And_Statements_Should_Work_EndToEnd()
    {
        await using var host = await TestCardsApiHost.StartAsync();
        var client = host.App.GetTestClient();

        await host.SeedAsync(db =>
        {
            db.CardBrands.Add(new CardBrand(1, "Visa", "visa"));
            return Task.CompletedTask;
        });

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cards")
        {
            Content = JsonContent.Create(new CardRequest(1, "Henrique Santos", "1234", "Visa principal", "Banco X", 5000m, 10, 18))
        };
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        var createResponse = await client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CardResponse>();
        created.Should().NotBeNull();
        created!.Nickname.Should().Be("Visa principal");

        var closeDate = new DateOnly(2026, 3, 10);
        var dueDate = new DateOnly(2026, 3, 18);

        await host.SeedAsync(db =>
        {
            var plan = new MoneyPlan(
                TestCardsApiHost.TestUserId,
                MoneyType.Expense,
                "Mercado mensal",
                300m,
                ScheduleType.OneTime,
                new DateOnly(2026, 3, 5),
                cardId: created.Id);
            db.MoneyPlans.Add(plan);

            var installment = new MoneyInstallment(
                plan.Id,
                TestCardsApiHost.TestUserId,
                1,
                dueDate,
                300m,
                statementYear: 2026,
                statementMonth: 3,
                statementCloseDate: closeDate,
                statementDueDate: dueDate);
            db.MoneyInstallments.Add(installment);
            db.MoneyPayments.Add(new MoneyPayment(installment.Id, TestCardsApiHost.TestUserId, DateTime.UtcNow, 120m));
            return Task.CompletedTask;
        });

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cards?page=1&pageSize=10&sortBy=nickname");
        listRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var listResponse = await client.SendAsync(listRequest);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cards = await listResponse.Content.ReadFromJsonAsync<List<CardResponse>>();
        cards.Should().NotBeNull();
        cards.Should().ContainSingle(x => x.Id == created.Id && x.Last4 == "1234");

        var debtRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cards/debt/total");
        debtRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var debtResponse = await client.SendAsync(debtRequest);

        debtResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var debt = await debtResponse.Content.ReadFromJsonAsync<TotalDebtEnvelope>();
        debt.Should().NotBeNull();
        debt!.Total.Should().Be(300m);

        var statementsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cards/{created.Id}/statements?year=2026&month=3");
        statementsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        var statementsResponse = await client.SendAsync(statementsRequest);

        statementsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statements = await statementsResponse.Content.ReadFromJsonAsync<List<CardStatementCycleResponse>>();
        statements.Should().NotBeNull();
        statements.Should().ContainSingle();
        statements![0].TotalAmount.Should().Be(300m);
        statements[0].TotalPaid.Should().Be(120m);
        statements[0].TotalOpen.Should().Be(180m);
        statements[0].Items.Should().ContainSingle(x => x.Title == "Mercado mensal" && x.OpenAmount == 180m);
    }

    private sealed record TotalDebtEnvelope(decimal Total);

    private sealed class TestCardsApiHost(WebApplication app, SqliteConnection connection) : IAsyncDisposable
    {
        internal static readonly Guid TestUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        public WebApplication App { get; } = app;

        public static async Task<TestCardsApiHost> StartAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            builder.Services.AddControllers().AddApplicationPart(typeof(CardsController).Assembly);
            builder.Services.AddDbContext<InvestDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<IInvestDbContext>(sp => sp.GetRequiredService<InvestDbContext>());

            builder.Services.AddScoped<ICardRepository, CardRepository>();
            builder.Services.AddScoped<ICardBrandRepository, CardBrandRepository>();
            builder.Services.AddScoped<IMoneyInstallmentRepository, MoneyInstallmentRepository>();
            builder.Services.AddScoped<IMoneyPaymentRepository, MoneyPaymentRepository>();
            builder.Services.AddScoped<IMoneyPlanRepository, MoneyPlanRepository>();
            builder.Services.AddScoped<ICardsService, CardsService>();
            builder.Services.AddSingleton<IAuditService, NoOpAuditService>();

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
            return new TestCardsApiHost(app, connection);
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
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestCardsApiHost.TestUserId.ToString()),
                new Claim(ClaimTypes.Role, UserRole.Intermediate.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
