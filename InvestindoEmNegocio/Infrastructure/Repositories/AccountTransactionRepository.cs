using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class AccountTransactionRepository(InvestDbContext context) : IAccountTransactionRepository
{
    public async Task<List<AccountTransaction>> ListByAccountAsync(
        Guid accountId,
        Guid userId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.AccountTransactions.AsNoTracking()
            .Where(t => t.AccountId == accountId && t.UserId == userId);

        if (fromUtc.HasValue) query = query.Where(t => t.OccurredAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(t => t.OccurredAt <= toUtc.Value);

        return await query.OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<AccountTransaction> Items, int TotalCount)> ListByAccountPagedAsync(
        Guid accountId,
        Guid userId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        var query = context.AccountTransactions.AsNoTracking()
            .Where(t => t.AccountId == accountId && t.UserId == userId);

        if (fromUtc.HasValue) query = query.Where(t => t.OccurredAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(t => t.OccurredAt <= toUtc.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<decimal> SumSignedAmountByAccountAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default)
    {
        var signedAmounts = await context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && t.UserId == userId)
            .Select(t => t.Kind == AccountTransactionKind.Credit ? t.Amount : -t.Amount)
            .ToListAsync(cancellationToken);

        return signedAmounts.Sum();
    }

    public async Task<List<AccountTransaction>> ListBySourceAsync(
        Guid userId,
        string sourceType,
        IEnumerable<Guid> sourceIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sourceIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await context.AccountTransactions
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId.HasValue && ids.Contains(t.SourceId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AccountTransaction transaction, CancellationToken cancellationToken = default)
    {
        await context.AccountTransactions.AddAsync(transaction, cancellationToken);
    }

    public void RemoveRange(IEnumerable<AccountTransaction> transactions)
    {
        context.AccountTransactions.RemoveRange(transactions);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
