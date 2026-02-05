using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Infrastructure.Data;

public static class DbMigrationExtensions
{
    public static async Task ApplyDatabaseSchemaAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvestDbContext>>();
        var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        try
        {
            // Keep EF model mapping, but stop relying on migrations history.
            var created = await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("EnsureCreated executado. Banco criado agora? {Created}", created);

            var schemaPath = Path.Combine(hostEnvironment.ContentRootPath, "Infrastructure", "Data", "schema.sql");
            if (!File.Exists(schemaPath))
            {
                logger.LogWarning("schema.sql não encontrado em {Path}.", schemaPath);
                return;
            }

            var schemaSql = await File.ReadAllTextAsync(schemaPath);
            if (string.IsNullOrWhiteSpace(schemaSql))
            {
                logger.LogWarning("schema.sql está vazio em {Path}.", schemaPath);
                return;
            }

            await dbContext.Database.ExecuteSqlRawAsync(schemaSql);
            logger.LogInformation("schema.sql aplicado com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao aplicar schema.sql no banco.");
            throw;
        }
    }
}
