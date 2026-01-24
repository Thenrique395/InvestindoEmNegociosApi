using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly InvestDbContext _context;

    public InstitutionRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<Institution>> ListActiveAsync(InstitutionType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Institutions.AsNoTracking().Where(i => i.IsActive);
        if (type.HasValue)
        {
            query = query.Where(i => i.Type == type.Value);
        }

        return await query.OrderBy(i => i.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Institution>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Institutions.AsNoTracking()
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Institution?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Institutions.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, InstitutionType type, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToUpperInvariant();
        return await _context.Institutions.AsNoTracking()
            .AnyAsync(i => i.Type == type && i.Name.ToUpper() == normalized, cancellationToken);
    }

    public async Task AddAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        await _context.Institutions.AddAsync(institution, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
