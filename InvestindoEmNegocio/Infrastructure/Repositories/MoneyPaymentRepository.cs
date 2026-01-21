using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class MoneyPaymentRepository : IMoneyPaymentRepository
{
    private readonly InvestDbContext _context;

    public MoneyPaymentRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<MoneyPayment>> ListByInstallmentIdAsync(Guid installmentId, CancellationToken cancellationToken = default)
    {
        return await _context.MoneyPayments
            .Where(p => p.InstallmentId == installmentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MoneyPayment>> ListByInstallmentIdsAsync(IEnumerable<Guid> installmentIds, CancellationToken cancellationToken = default)
    {
        var ids = installmentIds.ToList();
        if (ids.Count == 0) return new List<MoneyPayment>();

        return await _context.MoneyPayments
            .Where(p => ids.Contains(p.InstallmentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> SumPaidAmountAsync(Guid installmentId, CancellationToken cancellationToken = default)
    {
        return await _context.MoneyPayments
            .Where(p => p.InstallmentId == installmentId)
            .SumAsync(p => p.PaidAmount, cancellationToken);
    }

    public async Task AddAsync(MoneyPayment payment, CancellationToken cancellationToken = default)
    {
        await _context.MoneyPayments.AddAsync(payment, cancellationToken);
    }

    public void RemoveRange(IEnumerable<MoneyPayment> payments)
    {
        _context.MoneyPayments.RemoveRange(payments);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
