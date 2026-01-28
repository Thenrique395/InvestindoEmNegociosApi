using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class NotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly InvestDbContext _context;

    public NotificationSettingsRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _context.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotificationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _context.NotificationSettings.FirstOrDefaultAsync(cancellationToken);
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
        await _context.NotificationSettings.AddAsync(settings, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task AddAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
    {
        await _context.NotificationSettings.AddAsync(settings, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
