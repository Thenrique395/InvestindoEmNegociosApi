namespace InvestindoEmNegocio.Application.Interfaces;

public interface IRobotTask
{
    string Name { get; }
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
