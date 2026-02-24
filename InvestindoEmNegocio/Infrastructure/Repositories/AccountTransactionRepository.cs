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

    public async Task<decimal> SumSignedAmountByAccountAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && t.UserId == userId)
            .SumAsync(t => t.Kind == AccountTransactionKind.Credit ? t.Amount : -t.Amount, cancellationToken);
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
