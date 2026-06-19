using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/loans")]
[Route("api/v1/loans")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureLoansAccess)]
public class LoansController(ILoansService loansService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await loansService.ListAsync(userId, cancellationToken));
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.SimulateAsync(userId, request, cancellationToken));
        }, "Contrato inválido");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Created("", await loansService.CreateAsync(userId, request, cancellationToken));
        }, "Contrato inválido");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.UpdateAsync(userId, id, request, cancellationToken));
        }, "Contrato inválido", invalidOperationTitle: "Edição não permitida", invalidOperationStatusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await loansService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }

}
