using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly InvestDbContext _context;

    public PaymentMethodRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<PaymentMethod>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PaymentMethods.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PaymentMethod>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PaymentMethods.AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentMethod?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
