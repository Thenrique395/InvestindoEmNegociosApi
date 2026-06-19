using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IScenariosService
{
    Task<ScenarioSimulationResponse> SimulateAsync(Guid userId, ScenarioSimulationRequest request, CancellationToken cancellationToken = default);
}
