using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyPlanRepository : IMoneyPlanRepository
{
    private readonly InvestDbContext _context;

    public MoneyPlanRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<MoneyPlan>> ListByUserAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var query = _context.MoneyPlans.AsNoTracking().Where(p => p.UserId == userId);
        if (type.HasValue) query = query.Where(p => p.Type == type.Value);
        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<MoneyPlan?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.MoneyPlans.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(MoneyPlan plan, CancellationToken cancellationToken = default)
    {
        await _context.MoneyPlans.AddAsync(plan, cancellationToken);
    }

    public void Remove(MoneyPlan plan)
    {
        _context.MoneyPlans.Remove(plan);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
