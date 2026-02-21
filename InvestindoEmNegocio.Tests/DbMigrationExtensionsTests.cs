using FluentAssertions;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Tests;

public class DbMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDatabaseSchemaAsync_Should_Return_When_Schema_File_Does_Not_Exist()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "invest-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var app = BuildApp(tempRoot);

            var act = async () => await app.ApplyDatabaseSchemaAsync();

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyDatabaseSchemaAsync_Should_Apply_Schema_When_File_Exists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "invest-tests-" + Guid.NewGuid().ToString("N"));
        var schemaDir = Path.Combine(tempRoot, "Infrastructure", "Data");
        Directory.CreateDirectory(schemaDir);
        var schemaPath = Path.Combine(schemaDir, "schema.sql");
        await File.WriteAllTextAsync(schemaPath, "CREATE TABLE IF NOT EXISTS test_probe (id INTEGER PRIMARY KEY);");

        try
        {
            var app = BuildApp(tempRoot);

            var act = async () => await app.ApplyDatabaseSchemaAsync();

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static WebApplication BuildApp(string contentRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
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
