using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyInstallmentRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IMoneyInstallmentRepository
{
    public async Task<List<MoneyInstallment>> ListByUserAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var query = context.MoneyInstallments.AsNoTracking().Where(i => i.UserId == userId && (!spaceId.HasValue || i.SpaceId == spaceId.Value));

        if (status.HasValue) query = query.Where(i => i.Status == status);
        if (from.HasValue) query = query.Where(i => i.DueDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.DueDate <= to.Value);
        if (type.HasValue)
            query = query.Join(context.MoneyPlans.Where(p => p.Type == type.Value), i => i.PlanId, p => p.Id, (i, _) => i);

        return await query.OrderBy(i => i.DueDate).ThenBy(i => i.InstallmentNo).ToListAsync(cancellationToken);
    }

    public async Task<List<MoneyInstallment>> ListByUserStatusesAsync(Guid userId, IReadOnlyCollection<InstallmentStatus> statuses, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var query = context.MoneyInstallments.AsNoTracking().Where(i => i.UserId == userId && (!spaceId.HasValue || i.SpaceId == spaceId.Value));

        if (statuses is { Count: > 0 }) query = query.Where(i => statuses.Contains(i.Status));
        if (from.HasValue) query = query.Where(i => i.DueDate >= from.Value);
        if (to.HasValue) query = query.Where(i => i.DueDate <= to.Value);
        if (type.HasValue)
            query = query.Join(context.MoneyPlans.Where(p => p.Type == type.Value), i => i.PlanId, p => p.Id, (i, _) => i);

        return await query.OrderBy(i => i.DueDate).ThenBy(i => i.InstallmentNo).ToListAsync(cancellationToken);
    }

    public async Task<List<MoneyInstallment>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default, bool track = false)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var query = track
            ? context.MoneyInstallments.Where(i => i.PlanId == planId && i.UserId == userId && (!spaceId.HasValue || i.SpaceId == spaceId.Value))
            : context.MoneyInstallments.AsNoTracking().Where(i => i.PlanId == planId && i.UserId == userId && (!spaceId.HasValue || i.SpaceId == spaceId.Value));

        return await query.OrderBy(i => i.InstallmentNo).ToListAsync(cancellationToken);
    }

    public async Task<decimal> SumCardDebtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        var amounts = await (from installment in context.MoneyInstallments.AsNoTracking()
            join plan in context.MoneyPlans.AsNoTracking() on installment.PlanId equals plan.Id
            where installment.UserId == userId
                  && plan.UserId == userId
                  && (!spaceId.HasValue || installment.SpaceId == spaceId.Value)
                  && plan.Type == MoneyType.Expense
                  && plan.CardId != null
                  && installment.Status != InstallmentStatus.Paid
                  && installment.Status != InstallmentStatus.Canceled
            select installment.Amount
            ).ToListAsync(cancellationToken);

        return amounts.Sum();
    }

    public async Task<MoneyInstallment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.MoneyInstallments.FirstOrDefaultAsync(
            i => i.Id == id && (!spaceId.HasValue || i.SpaceId == spaceId.Value),
            cancellationToken);
    }

    public async Task AddAsync(MoneyInstallment installment, CancellationToken cancellationToken = default)
    {
        await context.MoneyInstallments.AddAsync(installment, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<MoneyInstallment> installments, CancellationToken cancellationToken = default)
    {
        await context.MoneyInstallments.AddRangeAsync(installments, cancellationToken);
    }

    public void Remove(MoneyInstallment installment)
    {
        context.MoneyInstallments.Remove(installment);
    }

    public void RemoveRange(IEnumerable<MoneyInstallment> installments)
    {
        context.MoneyInstallments.RemoveRange(installments);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
