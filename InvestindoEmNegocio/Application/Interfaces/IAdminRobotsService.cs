using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRobotsService
{
    Task<RobotMonitorResponseDto> MonitorAsync(int take = 50, CancellationToken cancellationToken = default);
    Task<RobotRunResultDto?> RunAsync(string robotName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(CancellationToken cancellationToken = default);
}
