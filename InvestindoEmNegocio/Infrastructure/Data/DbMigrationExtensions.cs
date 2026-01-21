using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Infrastructure.Data;

public static class DbMigrationExtensions
{
    /// <summary>
    /// Aplica migrações pendentes no start. Se não houver, apenas segue.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvestDbContext>>();

        try
        {
            var allMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

            logger.LogInformation("Migrações encontradas: {All}", string.Join(", ", allMigrations));
            logger.LogInformation("Migrações já aplicadas: {Applied}", string.Join(", ", applied));

            if (pending.Any())
            {
                logger.LogInformation("Aplicando migrações pendentes: {Migrations}", string.Join(", ", pending));
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrações aplicadas com sucesso.");
            }
            else
            {
                logger.LogInformation("Nenhuma migração pendente encontrada.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao aplicar migrações do banco.");
            throw;
        }
    }
}
