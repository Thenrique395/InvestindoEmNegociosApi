using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Infrastructure.Data;

public static class DbMigrationExtensions
{
    // Aplica migrations pendentes no boot — fonte ÚNICA de verdade do schema. Substituiu o
    // antigo EnsureCreated() + ExecuteSqlRaw(schema.sql), dual-mecanismo que causava drift
    // (origem dos bugs login-500 e Goal Mode). Idempotente: só aplica o que falta.
    public static async Task ApplyDatabaseSchemaAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvestDbContext>>();

        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation(
                    "Aplicando {Count} migration(s) pendente(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
            }

            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations aplicadas com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao aplicar migrations no banco.");
            throw;
        }
    }
}
