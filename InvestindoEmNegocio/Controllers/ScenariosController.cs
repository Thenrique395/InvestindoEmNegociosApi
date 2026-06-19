using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/scenarios")]
[Route("api/v1/scenarios")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureScenariosAccess)]
public class ScenariosController(IScenariosService scenariosService) : AuthenticatedControllerBase
{
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] ScenarioSimulationRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await scenariosService.SimulateAsync(userId, request, cancellationToken));
        }, "Parâmetros de simulação inválidos");
    }
}
