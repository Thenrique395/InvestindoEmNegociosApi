using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IProfileQueryService
{
    Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
