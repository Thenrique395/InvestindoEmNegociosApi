using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Tests;

public class ReferenceDataSeedExtensionsTests
{
    [Fact]
    public async Task SeedReferenceDataAsync_Should_Create_Default_Categories_And_Be_Idempotent()
    {
        var app = BuildApp();
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        await app.SeedReferenceDataAsync();
        await app.SeedReferenceDataAsync();

        var categories = await dbContext.Categories.AsNoTracking().ToListAsync();
        categories.Should().HaveCount(17);
        categories.Should().Contain(c => c.UserId == null && c.Name == "Salário" && c.AppliesTo == MoneyType.Income && c.IsActive);
        categories.Should().Contain(c => c.UserId == null && c.Name == "Alimentação" && c.AppliesTo == MoneyType.Expense && c.IsActive);
    }

    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        builder.Services.AddSingleton(connection);
        builder.Services.AddDbContext<InvestDbContext>((sp, options) =>
        {
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
        });
        builder.Services.AddLogging(config => config.AddDebug().SetMinimumLevel(LogLevel.Debug));

        return builder.Build();
    }
}
