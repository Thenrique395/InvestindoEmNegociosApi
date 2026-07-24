using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class NotificationSettingsRepository(InvestDbContext context) : INotificationSettingsRepository
{
    public async Task<NotificationSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        // Tabela singleton, mas OrderBy torna o First determinístico (evita o warning
        // FirstWithoutOrderByAndFilterWarning e garante resultado estável).
        return await context.NotificationSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotificationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var existing = await context.NotificationSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;
        var settings = new NotificationSettings(
            incomeUpcomingEnabled: true,
            incomeDaysBefore: 2,
            expenseUpcomingEnabled: true,
            expenseDaysBefore: 2,
            expenseOverdueEnabled: true,
            cardCloseSoonEnabled: true,
            cardCloseDaysBefore: 2,
            cardCloseDayEnabled: true,
            monthCloseEnabled: true,
            monthSummaryEnabled: true,
            goalBelowExpectedEnabled: true,
            goalCompletedEnabled: true,
            goalInactivityEnabled: true,
            goalInactivityDays: 30);
        await context.NotificationSettings.AddAsync(settings, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task AddAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
    {
        await context.NotificationSettings.AddAsync(settings, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
