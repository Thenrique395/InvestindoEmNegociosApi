using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ISpaceBootstrapService
{
    Task<Guid> EnsureDefaultSpaceAsync(User user, CancellationToken cancellationToken = default);
}
