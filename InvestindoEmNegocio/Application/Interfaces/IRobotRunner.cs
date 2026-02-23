using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IRobotRunner
{
    Task<RobotRunResultDto?> RunByNameAsync(string robotName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(CancellationToken cancellationToken = default);
}
