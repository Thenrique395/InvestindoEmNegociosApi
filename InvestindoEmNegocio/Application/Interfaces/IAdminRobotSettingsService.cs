using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRobotSettingsService
{
    Task<RobotSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<RobotSettingsDto> UpdateAsync(UpdateRobotSettingsRequest request, CancellationToken cancellationToken = default);
}
