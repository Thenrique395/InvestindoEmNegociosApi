using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using InvestindoEmNegocio.Infrastructure.Services;

namespace InvestindoEmNegocio.Tests;

public class CorrectionRegressionTests
{
    private static readonly DateOnly Due = new(2026, 11, 15);
    private static ICurrentSpaceAccessor Space(Guid id) => Mock.Of<ICurrentSpaceAccessor>(x => x.SpaceId == id && x.RequireSpaceId() == id);
    private static MonthlyFinancialSnapshot Snapshot(Guid user, Guid space) => new(user, space, 2026, 11, 10m, 20m, 0m, 0m, 0m, 30m, 10, "ok", "Teste", "[]");
    private static BudgetService Budget(InvestDbContext db, Guid space) => new(new MonthlyBudgetRepository(db, Space(space)), new MoneyInstallmentRepository(db, Space(space)), new MoneyPlanRepository(db, Space(space)), new CategoryRepository(db), Space(space));
    private static InstallmentsService Installments(InvestDbContext db, Guid space) => new(new MoneyInstallmentRepository(db, Space(space)), new MoneyPaymentRepository(db, Space(space)), new MoneyPlanRepository(db, Space(space)), new UserRepository(db), new AccountRepository(db, Space(space)), new AccountTransactionRepository(db, Space(space)), Mock.Of<IReceiptStorageService>(), Mock.Of<IPlanHistoryService>(), NullLogger<InstallmentsService>.Instance);

    [Fact]
    public async Task Budget_Should_Persist_Update_Across_Contexts_And_Isolate_Spaces()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;
        var user = Guid.NewGuid(); var areaA = Guid.NewGuid(); var areaB = Guid.NewGuid();
        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            await Budget(db, areaA).UpsertItemsAsync(user, 2026, 11, [new BudgetItemRequest("Mercado", 100m)]);
            await Budget(db, areaB).UpsertItemsAsync(user, 2026, 11, [new BudgetItemRequest("Mercado", 900m)]);
        }
        await using (var db = new InvestDbContext(options))
            await Budget(db, areaA).UpsertItemsAsync(user, 2026, 11, [new BudgetItemRequest("Mercado", 200m)]);
        await using (var db = new InvestDbContext(options))
        {
            (await Budget(db, areaA).GetAsync(user, 2026, 11)).Items.Single().PlannedAmount.Should().Be(200m);
            (await Budget(db, areaB).GetAsync(user, 2026, 11)).Items.Single().PlannedAmount.Should().Be(900m);
            var foreign = await new MonthlyBudgetRepository(db, Space(areaB)).GetByIdAsync((await db.MonthlyBudgetItems.FirstAsync(x => x.SpaceId == areaA)).Id, user);
            foreign.Should().BeNull();
        }
    }

    [Fact]
    public async Task Background_Space_Scope_Should_Restore_Parent_Across_Await()
    {
        var accessor = new CurrentSpaceAccessor(new HttpContextAccessor());
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        accessor.SpaceId.Should().BeNull();
        using (accessor.BeginScope(first))
        {
            await Task.Yield();
            accessor.RequireSpaceId().Should().Be(first);
            using (accessor.BeginScope(second))
            {
                await Task.Yield();
                accessor.RequireSpaceId().Should().Be(second);
            }
            accessor.RequireSpaceId().Should().Be(first);
        }
        accessor.SpaceId.Should().BeNull();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task Paid_Installment_Edit_And_Delete_Should_Preserve_Ledger(bool delete, bool withoutAccount, bool deletePlan)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;
        var user = Guid.NewGuid(); var area = Guid.NewGuid(); Guid id;
        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var account = new Account(user, area, "Conta", AccountType.Checking, 0m);
            var plan = new MoneyPlan(user, area, MoneyType.Expense, "Mercado", 100m, ScheduleType.OneTime, Due);
            var installment = new MoneyInstallment(plan.Id, user, area, 1, Due, 100m);
            var payment = new MoneyPayment(installment.Id, user, area, DateTime.UtcNow, 100m, accountId: withoutAccount ? null : account.Id);
            installment.RefreshPaymentStatus(100m);
            db.Accounts.Add(account); db.MoneyPlans.Add(plan); db.MoneyInstallments.Add(installment); db.MoneyPayments.Add(payment);
            if (!withoutAccount) db.AccountTransactions.Add(new AccountTransaction(account.Id, user, area, DateTime.UtcNow, AccountTransactionKind.Debit, 100m, "Pagamento", "InstallmentPayment", payment.Id));
            await db.SaveChangesAsync(); id = installment.Id;
        }
        await using (var db = new InvestDbContext(options))
            (await Installments(db, area).UpdateAsync(user, id, new UpdateInstallmentRequest(80m, Due))).Should().BeTrue();
        await using (var db = new InvestDbContext(options))
        {
            (await db.MoneyInstallments.SingleAsync()).Status.Should().Be(InstallmentStatus.Open);
            (await db.MoneyPayments.ToListAsync()).Sum(x => x.PaidAmount).Should().Be(0m);
            (await db.AccountTransactions.ToListAsync()).Sum(x => x.Kind == AccountTransactionKind.Credit ? x.Amount : -x.Amount).Should().Be(0m);
            (await Installments(db, area).UpdateAsync(user, id, new UpdateInstallmentRequest(70m, Due))).Should().BeTrue();
            (await db.MoneyPayments.ToListAsync()).Sum(x => x.PaidAmount).Should().Be(0m);
            if (deletePlan)
            {
                var planId = (await db.MoneyInstallments.SingleAsync()).PlanId;
                var plans = new PlansService(new MoneyPlanRepository(db, Space(area)), new MoneyInstallmentRepository(db, Space(area)), new MoneyPaymentRepository(db, Space(area)), new AccountTransactionRepository(db, Space(area)), Mock.Of<ICardRepository>(), Mock.Of<ICategoryRepository>(), Space(area), Mock.Of<IPlanHistoryService>(), NullLogger<PlansService>.Instance);
                (await plans.DeleteAsync(user, planId)).Should().BeTrue();
            }
            else if (delete) (await Installments(db, area).DeleteAsync(user, id)).Should().BeTrue();
        }
        if (delete)
        {
            await using var db = new InvestDbContext(options);
            (await db.AccountTransactions.ToListAsync()).Should().BeEmpty();
            (await db.MoneyPayments.ToListAsync()).Should().BeEmpty();
            (await db.MoneyInstallments.ToListAsync()).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Delete_Account_Should_Remove_New_Operational_Entities_And_Preserve_Other_User()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;
        await using var db = new InvestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var user = new User("Titular", "audit@example.invalid", BCrypt.Net.BCrypt.HashPassword("audit-pass"));
        var other = new User("Outro", "other@example.invalid", "unused");
        var area = new Space(user.Id, "Pessoal", true);
        var otherArea = new Space(other.Id, "Outro", true);
        var loan = new LoanContract(user.Id, area.Id, "Contrato", 12000m, 18m, 0.015m, InterestRatePeriod.AnnualNominal, 12, LoanAmortizationType.Price, Due, 15, 1100m, 13200m, 1200m, 13200m);
        db.Users.AddRange(user, other); db.Spaces.AddRange(area, otherArea);
        db.MonthlyBudgetItems.Add(new MonthlyBudgetItem(user.Id, area.Id, 2026, 11, "Mercado", 100m));
        db.MonthlyFinancialSnapshots.Add(Snapshot(user.Id, area.Id));
        db.EmailConfirmationTokens.Add(new EmailConfirmationToken(user.Id, "hash", DateTime.UtcNow.AddDays(1)));
        db.LoanContracts.Add(loan);
        db.LoanInstallments.Add(new LoanInstallment(loan.Id, user.Id, 1, Due, 12000m, 900m, 200m, 1100m, 11000m));
        await db.SaveChangesAsync();
        var cleanup = new Mock<IUserFileCleanupService>();
        var service = new UserPrivacyCenterService(new UserRepository(db), new RefreshTokenRepository(db), Mock.Of<IAuditService>(), db, cleanup.Object);
        await service.DeleteOwnAccountAsync(user.Id, new DeleteOwnAccountRequest("audit-pass", "EXCLUIR"));
        user.IsAnonymized.Should().BeTrue();
        (await db.Spaces.IgnoreQueryFilters().CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await db.MonthlyBudgetItems.CountAsync()).Should().Be(0);
        (await db.MonthlyFinancialSnapshots.CountAsync()).Should().Be(0);
        (await db.EmailConfirmationTokens.CountAsync()).Should().Be(0);
        (await db.LoanContracts.CountAsync()).Should().Be(0);
        (await db.LoanInstallments.CountAsync()).Should().Be(0);
        (await db.Spaces.CountAsync(x => x.UserId == other.Id)).Should().Be(1);
        cleanup.Verify(x => x.DeleteUserFilesAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Snapshot_Repository_Should_Isolate_Identical_Months_Per_Space()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = Guid.NewGuid(); var areaA = Guid.NewGuid(); var areaB = Guid.NewGuid();
        var a = Snapshot(user, areaA); var b = Snapshot(user, areaB);
        db.MonthlyFinancialSnapshots.AddRange(a, b); await db.SaveChangesAsync();
        (await new MonthlyFinancialSnapshotRepository(db, Space(areaA)).GetByMonthAsync(user, 2026, 11))!.Id.Should().Be(a.Id);
        (await new MonthlyFinancialSnapshotRepository(db, Space(areaB)).GetByMonthAsync(user, 2026, 11))!.Id.Should().Be(b.Id);
    }

    [Fact]
    public async Task Receipt_Should_Require_The_Payment_Owner_And_Active_Space()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = Guid.NewGuid(); var area = Guid.NewGuid();
        var plan = new MoneyPlan(user, area, MoneyType.Expense, "Teste", 100m, ScheduleType.OneTime, Due);
        var installment = new MoneyInstallment(plan.Id, user, area, 1, Due, 100m);
        var payment = new MoneyPayment(installment.Id, user, area, DateTime.UtcNow, 100m);
        var file = $"{user:N}_{Guid.NewGuid():N}.pdf";
        payment.AttachReceipt("https://api.example.invalid/api/v1/receipts/" + file);
        db.MoneyPlans.Add(plan); db.MoneyInstallments.Add(installment); db.MoneyPayments.Add(payment); await db.SaveChangesAsync();
        var storage = new Mock<IReceiptStorageService>();
        storage.Setup(x => x.OpenAsync(user, file, It.IsAny<CancellationToken>())).ReturnsAsync(new ReceiptFile(new MemoryStream([1]), "application/pdf"));
        (await new ReceiptAccessService(db, Space(area), storage.Object).OpenAsync(Guid.NewGuid(), file)).Should().BeNull();
        (await new ReceiptAccessService(db, Space(Guid.NewGuid()), storage.Object).OpenAsync(user, file)).Should().BeNull();
        storage.Verify(x => x.OpenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await new ReceiptAccessService(db, Space(area), storage.Object).OpenAsync(user, file)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_Jwt_Should_Be_Rejected_Immediately_After_Revocation_Or_Downgrade(bool downgrade)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = new User("Titular", "jwt@example.invalid", "unused"); user.SetRole(UserRole.Admin);
        db.Users.Add(user); await db.SaveChangesAsync();
        var jwt = new JwtOptions { Issuer = "audit", Audience = "audit", SecretKey = new string('x', 64), ExpiresMinutes = 15 };
        var token = new JwtTokenGenerator(Options.Create(jwt)).Generate(user, Guid.NewGuid());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = jwt.Issuer, ["Jwt:Audience"] = jwt.Audience, ["Jwt:SecretKey"] = jwt.SecretKey }).Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IUserRepository>(new UserRepository(db));
        builder.Services.AddJwtAuthentication(configuration);
        builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);
        builder.Services.AddControllers().AddApplicationPart(typeof(RevocationProbeController).Assembly);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        (await client.GetAsync("/test/revocation-probe")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        if (downgrade) await new AdminUsersService(new UserRepository(db), Mock.Of<IUserSubscriptionRepository>(), Mock.Of<IUserFeatureOverrideRepository>(), Mock.Of<IUserSessionService>(), Mock.Of<IAuditService>()).UpdateRoleAsync(user.Id, "Basic", default);
        else await new UserPrivacyCenterService(new UserRepository(db), new RefreshTokenRepository(db), Mock.Of<IAuditService>(), db, Mock.Of<IUserFileCleanupService>()).RevokeOwnSessionsAsync(user.Id);
        (await client.GetAsync("/test/revocation-probe")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Receipt_Http_Should_Block_Anonymous_Legacy_Static_And_Foreign_Access()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:"); await connection.OpenAsync();
        await using var db = new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = new User("Titular", "owner@example.invalid", "unused");
        var foreign = new User("Outro", "foreign@example.invalid", "unused");
        var area = Guid.NewGuid(); var fileName = $"{owner.Id:N}_{Guid.NewGuid():N}.pdf";
        var plan = new MoneyPlan(owner.Id, area, MoneyType.Expense, "Teste", 100m, ScheduleType.OneTime, Due);
        var installment = new MoneyInstallment(plan.Id, owner.Id, area, 1, Due, 100m);
        var payment = new MoneyPayment(installment.Id, owner.Id, area, DateTime.UtcNow, 100m);
        payment.AttachReceipt("https://api.example.invalid/uploads/receipts/" + fileName);
        db.Users.AddRange(owner, foreign); db.MoneyPlans.Add(plan); db.MoneyInstallments.Add(installment); db.MoneyPayments.Add(payment); await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "ien-receipts-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, "wwwroot", "uploads", "receipts"); Directory.CreateDirectory(legacy);
        await File.WriteAllBytesAsync(Path.Combine(legacy, fileName), [1, 2, 3]);
        var jwt = new JwtOptions { Issuer = "receipt-audit", Audience = "receipt-audit", SecretKey = new string('y', 64) };
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = "wwwroot", EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Host.UseSerilog(new LoggerConfiguration().CreateLogger());
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = jwt.Issuer, ["Jwt:Audience"] = jwt.Audience, ["Jwt:SecretKey"] = jwt.SecretKey });
            builder.Services.AddApiSurface(true).AddAppRateLimiting().AddJwtAuthentication(builder.Configuration);
            builder.Services.AddCors(o => o.AddPolicy("audit", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);
            builder.Services.AddSingleton<IUserRepository>(new UserRepository(db)); builder.Services.AddSingleton<IInvestDbContext>(db);
            builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentSpaceAccessor, CurrentSpaceAccessor>();
            builder.Services.AddScoped<IReceiptAccessService, ReceiptAccessService>(); builder.Services.AddScoped<IReceiptStorageService, ReceiptStorageService>();
            builder.Services.AddControllers().AddApplicationPart(typeof(InvestindoEmNegocio.Controllers.ReceiptsController).Assembly).AddApplicationPart(typeof(RateLimitProbeController).Assembly);
            await using var app = builder.Build(); app.UseAppPipeline("audit"); app.MapControllers(); await app.StartAsync();
            using var client = app.GetTestClient();
            (await client.GetAsync("/uploads/receipts/" + fileName)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await client.GetAsync("/api/v1/receipts/" + fileName)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var generator = new JwtTokenGenerator(Options.Create(jwt));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.Generate(foreign, area).Token);
            (await client.GetAsync("/api/v1/receipts/" + fileName)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.Generate(owner, Guid.NewGuid()).Token);
            (await client.GetAsync("/api/v1/receipts/" + fileName)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.Generate(owner, area).Token);
            var receipt = await client.GetAsync("/uploads/receipts/" + fileName);
            receipt.StatusCode.Should().Be(HttpStatusCode.OK);
            (await receipt.Content.ReadAsByteArrayAsync()).Should().Equal([1, 2, 3]);
            receipt.Headers.CacheControl!.NoStore.Should().BeTrue();
            for (var request = 0; request < 5; request++)
                (await client.GetAsync("/test/rate-limit-probe")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await client.GetAsync("/test/rate-limit-probe")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generator.Generate(foreign, area).Token);
            (await client.GetAsync("/test/rate-limit-probe")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var cleanup = new UserFileCleanupService(app.Environment);
            await cleanup.DeleteUserFilesAsync(owner.Id);
            File.Exists(Path.Combine(legacy, fileName)).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

}

[ApiController]
[Route("test/revocation-probe")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminUsersManage)]
public sealed class RevocationProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => NoContent();
}

[ApiController]
[Route("test/rate-limit-probe")]
[Authorize]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("billing-checkout")]
public sealed class RateLimitProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => NoContent();
}
