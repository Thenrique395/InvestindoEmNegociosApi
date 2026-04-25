using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRobotExecutionService
{
    Task<RobotRunResultDto?> RunAsync(string robotName, bool force, int cooldownMinutes, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
}
