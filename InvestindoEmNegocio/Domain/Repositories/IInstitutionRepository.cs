using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IInstitutionRepository
{
    Task<List<Institution>> ListActiveAsync(InstitutionType? type = null, CancellationToken cancellationToken = default);
    Task<List<Institution>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<Institution?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, InstitutionType type, CancellationToken cancellationToken = default);
    Task AddAsync(Institution institution, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
