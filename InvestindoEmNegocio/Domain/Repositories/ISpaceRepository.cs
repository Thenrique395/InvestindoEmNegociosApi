using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ISpaceRepository
{
    Task<List<Space>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Space?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Space?> GetDefaultByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Space space, CancellationToken cancellationToken = default);
    void Remove(Space space);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
