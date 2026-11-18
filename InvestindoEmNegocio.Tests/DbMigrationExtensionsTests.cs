using FluentAssertions;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace InvestindoEmNegocio.Tests;

public class DbMigrationExtensionsTests
{
    [Fact]
    public void Migration_Model_Should_Match_The_Npgsql_Snapshot()
    {
        using var db = new InvestDbContextFactory().CreateDbContext([]);
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public void Idempotent_Migration_Script_Should_Include_Space_Backfill()
    {
        using var db = new InvestDbContextFactory().CreateDbContext([]);
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        script.Should().Contain("__EFMigrationsHistory").And.Contain("IsolateBudgetsAndSnapshotsBySpace");
        script.Should().Contain("UPDATE monthly_budget_items").And.Contain("UPDATE monthly_financial_snapshots");
    }

    [PostgresFact]
    public async Task Postgres_Bootstrap_Should_Be_Idempotent_And_Preserve_Legacy_Rows()
    {
        var connection = Environment.GetEnvironmentVariable("IEN_TEST_POSTGRES")!;
        var schema = "ien_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            var isolated = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<InvestDbContext>().UseNpgsql(isolated).Options;
            var user = Guid.NewGuid(); var budget = Guid.NewGuid(); var snapshot = Guid.NewGuid();
            await using (var db = new InvestDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20260827205714_PermitirEstornoComoPagamentoNegativo");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO monthly_budget_items (\"Id\", \"UserId\", \"Year\", \"Month\", \"CategoryName\", \"PlannedAmount\", \"CreatedAt\", \"UpdatedAt\") VALUES ({budget}, {user}, 2026, 11, 'Mercado', 123, NOW(), NOW())");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO monthly_financial_snapshots (\"Id\", \"UserId\", \"Year\", \"Month\", \"SnapshotLabel\", \"RealAvailableBalance\", \"ProjectedBalance\", \"PendingExpenses\", \"PendingIncomes\", \"TotalDebt\", \"NetWorth\", \"RiskScore\", \"RiskClassification\", \"PrimaryInsight\", \"RecommendationsJson\", \"CreatedAt\") VALUES ({snapshot}, {user}, 2026, 11, '11/2026', 0, 0, 0, 0, 0, 321, 0, 'ok', 'Teste', '[]', NOW())");
            }
            await using var services = new ServiceCollection().AddLogging().AddDbContext<InvestDbContext>(o => o.UseNpgsql(isolated)).BuildServiceProvider();
            var app = new ApplicationBuilder(services);
            await app.ApplyDatabaseSchemaAsync();
            await app.ApplyDatabaseSchemaAsync();
            await using var verified = new InvestDbContext(options);
            var space = await verified.Spaces.SingleAsync(x => x.UserId == user && x.IsDefault);
            (await verified.MonthlyBudgetItems.SingleAsync()).SpaceId.Should().Be(space.Id);
            (await verified.MonthlyBudgetItems.SingleAsync()).PlannedAmount.Should().Be(123m);
            (await verified.MonthlyFinancialSnapshots.SingleAsync()).SpaceId.Should().Be(space.Id);
            (await verified.MonthlyFinancialSnapshots.SingleAsync()).NetWorth.Should().Be(321m);
            (await verified.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IEN_TEST_POSTGRES")))
            Skip = "Configure IEN_TEST_POSTGRES para validar migrations em PostgreSQL isolado.";
    }
}
