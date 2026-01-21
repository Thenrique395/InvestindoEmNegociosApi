using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyInstallmentRepository : IMoneyInstallmentRepository
{
    private readonly InvestDbContext _context;

    public MoneyInstallmentRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<MoneyInstallment>> ListByUserAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var query = _context.MoneyInstallments.AsNoTracking().Where(i => i.UserId == userId);

        if (status.HasValue) query = query.Where(i => i.Status == status);
        if (from.HasValue) query = query.Where(i => i.DueDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.DueDate <= to.Value);
        if (type.HasValue)
        {
            query = query.Join(_context.MoneyPlans.Where(p => p.Type == type.Value), i => i.PlanId, p => p.Id, (i, _) => i);
        }

        return await query.OrderBy(i => i.DueDate).ThenBy(i => i.InstallmentNo).ToListAsync(cancellationToken);
    }

    public async Task<List<MoneyInstallment>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.MoneyInstallments.AsNoTracking()
            .Where(i => i.PlanId == planId && i.UserId == userId)
            .OrderBy(i => i.InstallmentNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> SumCardDebtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = from installment in _context.MoneyInstallments.AsNoTracking()
            join plan in _context.MoneyPlans.AsNoTracking() on installment.PlanId equals plan.Id
            where installment.UserId == userId
                  && plan.UserId == userId
                  && plan.Type == MoneyType.Expense
                  && plan.CardId != null
                  && installment.Status != InstallmentStatus.Paid
                  && installment.Status != InstallmentStatus.Canceled
            select installment.Amount;

        return await query.SumAsync(cancellationToken);
    }

    public async Task<MoneyInstallment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MoneyInstallments.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task AddAsync(MoneyInstallment installment, CancellationToken cancellationToken = default)
    {
        await _context.MoneyInstallments.AddAsync(installment, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<MoneyInstallment> installments, CancellationToken cancellationToken = default)
    {
        await _context.MoneyInstallments.AddRangeAsync(installments, cancellationToken);
    }

    public void Remove(MoneyInstallment installment)
    {
        _context.MoneyInstallments.Remove(installment);
    }

    public void RemoveRange(IEnumerable<MoneyInstallment> installments)
    {
        _context.MoneyInstallments.RemoveRange(installments);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
