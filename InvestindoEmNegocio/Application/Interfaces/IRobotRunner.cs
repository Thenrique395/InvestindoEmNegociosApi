using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IRobotRunner
{
    Task<RobotRunResultDto?> RunByNameAsync(string robotName, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
    Task<RobotRunResultDto?> RunSafelyByNameAsync(string robotName, int cooldownMinutes = 10, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default);
}
