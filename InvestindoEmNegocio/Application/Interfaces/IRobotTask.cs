using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IRobotTask
{
    string Name { get; }
    Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default);
}
