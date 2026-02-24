using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class AccountRepository(InvestDbContext context) : IAccountRepository
{
    public async Task<List<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Accounts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid userId, string name, Guid? ignoreId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return await context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId
                        && a.Name == normalized
                        && (!ignoreId.HasValue || a.Id != ignoreId.Value), cancellationToken);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await context.Accounts.AddAsync(account, cancellationToken);
    }

    public void Remove(Account account)
    {
        context.Accounts.Remove(account);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
