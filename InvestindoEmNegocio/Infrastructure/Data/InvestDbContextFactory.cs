using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvestindoEmNegocio.Infrastructure.Data;

/// <summary>Ferramentas de schema não inicializam a aplicação nem carregam segredos de outros ambientes.</summary>
public sealed class InvestDbContextFactory : IDesignTimeDbContextFactory<InvestDbContext>
{
    public InvestDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IEN_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Database=investindo_dev;Username=postgres";
        return new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseNpgsql(connection).Options);
    }
}
