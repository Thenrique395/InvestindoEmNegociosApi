using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public sealed class RobotSettingsRepository(InvestDbContext context) : IRobotSettingsRepository
{
    public async Task<RobotSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        // Singleton: OrderBy torna o First determinístico (evita FirstWithoutOrderBy…).
        return await context.RobotSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RobotSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var existing = await context.RobotSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var settings = new RobotSettings(enabled: true, dailyRunTimeUtc: "08:00");
        await context.RobotSettings.AddAsync(settings, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task AddAsync(RobotSettings settings, CancellationToken cancellationToken = default)
    {
        await context.RobotSettings.AddAsync(settings, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
