using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class UserNotificationRepository(InvestDbContext context) : IUserNotificationRepository
{
    public async Task<List<UserNotification>> ListByUserAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default)
    {
        var query = context.UserNotifications.AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        query = query.OrderByDescending(n => n.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<UserNotification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid userId, string referenceKey, CancellationToken cancellationToken = default)
    {
        return await context.UserNotifications.AsNoTracking()
            .AnyAsync(n => n.UserId == userId && n.ReferenceKey == referenceKey, cancellationToken);
    }

    public async Task<HashSet<string>> ListReferenceKeysAsync(Guid userId, IEnumerable<string> referenceKeys, CancellationToken cancellationToken = default)
    {
        var keys = referenceKeys?
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct()
            .ToList() ?? [];
        if (keys.Count == 0) return [];

        var existing = await context.UserNotifications.AsNoTracking()
            .Where(n => n.UserId == userId && keys.Contains(n.ReferenceKey))
            .Select(n => n.ReferenceKey)
            .ToListAsync(cancellationToken);
        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddRangeAsync(IEnumerable<UserNotification> notifications, CancellationToken cancellationToken = default)
    {
        await context.UserNotifications.AddRangeAsync(notifications, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
