using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvestindoEmNegocio.Infrastructure.Data;

// Fábrica usada APENAS pelas ferramentas de design-time do EF (dotnet ef migrations/update).
// Isola a geração de migrations do bootstrap completo do app (JWT, observabilidade, etc.),
// que exigiria segredos e conexões desnecessários. Nunca é usada em runtime — o app configura
// o DbContext via AddPersistence. A connection string aqui só configura o provider; nenhuma
// conexão é aberta para 'migrations add' (o modelo vem do OnModelCreating).
public sealed class InvestDbContextFactory : IDesignTimeDbContextFactory<InvestDbContext>
{
    public InvestDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=investindo_designtime;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(InvestDbContext).Assembly.GetName().Name))
            .Options;

        return new InvestDbContext(options);
    }
}
