using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class CategoryRepository(InvestDbContext context) : ICategoryRepository
{
    public async Task<List<Category>> ListForUserAsync(Guid userId, MoneyType? appliesTo, CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking()
            .Where(c => (c.UserId == null && c.IsActive) || c.UserId == userId);

        if (appliesTo.HasValue)
            query = query.Where(c => c.AppliesTo == null || c.AppliesTo == appliesTo.Value);

        return await query
            .OrderBy(c => c.UserId.HasValue)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task<List<Category>> ListDefaultsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking()
            .Where(c => c.UserId == null);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetDefaultByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == null, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking()
            .Where(c => c.UserId == userId && c.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> DefaultNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking()
            .Where(c => c.UserId == null && c.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await context.Categories.AddAsync(category, cancellationToken);
    }

    public void Remove(Category category)
    {
        context.Categories.Remove(category);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
