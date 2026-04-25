using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IUserAccountBootstrapService
{
    Task EnsureDefaultAccountForBasicAsync(User user, CancellationToken cancellationToken = default);
}
