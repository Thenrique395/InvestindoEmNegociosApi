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

    public async Task<List<PaymentMethod>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PaymentMethods.AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }
}
