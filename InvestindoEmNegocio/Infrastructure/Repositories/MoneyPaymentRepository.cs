using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyPaymentRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IMoneyPaymentRepository
{
    public async Task<MoneyPayment?> GetByIdAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.MoneyPayments
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && (!spaceId.HasValue || p.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task<List<MoneyPayment>> ListByInstallmentIdAsync(Guid installmentId, CancellationToken cancellationToken = default)
    {
        return await context.MoneyPayments
            .Where(p => p.InstallmentId == installmentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MoneyPayment>> ListByInstallmentIdsAsync(IEnumerable<Guid> installmentIds, CancellationToken cancellationToken = default)
    {
        var ids = installmentIds.ToList();
        if (ids.Count == 0) return new List<MoneyPayment>();

        return await context.MoneyPayments
            .Where(p => ids.Contains(p.InstallmentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> SumPaidAmountAsync(Guid installmentId, CancellationToken cancellationToken = default)
    {
        return await context.MoneyPayments
            .Where(p => p.InstallmentId == installmentId)
            .SumAsync(p => p.PaidAmount, cancellationToken);
    }

    public async Task AddAsync(MoneyPayment payment, CancellationToken cancellationToken = default)
    {
        await context.MoneyPayments.AddAsync(payment, cancellationToken);
    }

    public void RemoveRange(IEnumerable<MoneyPayment> payments)
    {
        context.MoneyPayments.RemoveRange(payments);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
