using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRobotMonitorService
{
    Task<RobotMonitorResponseDto> MonitorAsync(RobotMonitorQueryDto query, CancellationToken cancellationToken = default);
}
