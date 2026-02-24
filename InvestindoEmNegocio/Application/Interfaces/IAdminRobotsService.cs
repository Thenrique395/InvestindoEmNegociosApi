using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRobotsService
{
    Task<RobotMonitorResponseDto> MonitorAsync(RobotMonitorQueryDto query, CancellationToken cancellationToken = default);
    Task<RobotRunResultDto?> RunAsync(string robotName, bool force, int cooldownMinutes, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
}
