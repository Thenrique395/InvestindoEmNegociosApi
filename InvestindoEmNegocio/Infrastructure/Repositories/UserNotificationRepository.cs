using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class UserNotificationRepository : IUserNotificationRepository
{
    private readonly InvestDbContext _context;

    public UserNotificationRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserNotification>> ListByUserAsync(Guid userId, bool unreadOnly, int? limit, CancellationToken cancellationToken = default)
    {
        var query = _context.UserNotifications.AsNoTracking()
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
        return await _context.UserNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid userId, string referenceKey, CancellationToken cancellationToken = default)
    {
        return await _context.UserNotifications.AsNoTracking()
            .AnyAsync(n => n.UserId == userId && n.ReferenceKey == referenceKey, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<UserNotification> notifications, CancellationToken cancellationToken = default)
    {
        await _context.UserNotifications.AddRangeAsync(notifications, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
