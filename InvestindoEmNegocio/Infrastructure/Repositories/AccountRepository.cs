using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class AccountRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IAccountRepository
{
    public async Task<List<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Accounts.AsNoTracking()
            .Where(a => a.UserId == userId && (!spaceId.HasValue || a.SpaceId == spaceId.Value))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Account>> ListByUserAndSpaceAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken = default)
    {
        // Espaço EXPLÍCITO (não usa currentSpaceAccessor): no bootstrap de login o
        // espaço ambiente pode ser o de outra sessão. Rastreado de propósito para
        // que a reativação de conta (accounts[0].Activate()) seja persistida.
        return await context.Accounts
            .Where(a => a.UserId == userId && a.SpaceId == spaceId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Accounts.FirstOrDefaultAsync(
            a => a.Id == id && a.UserId == userId && (!spaceId.HasValue || a.SpaceId == spaceId.Value),
            cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid userId, string name, Guid? ignoreId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId
                        && (!spaceId.HasValue || a.SpaceId == spaceId.Value)
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
