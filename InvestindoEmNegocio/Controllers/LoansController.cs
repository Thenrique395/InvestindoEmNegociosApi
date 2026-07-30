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
public class LoansController(ILoansService loansService, ILoanTimelineService timelineService) : AuthenticatedControllerBase
{
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await timelineService.GetAsync(userId, id, cancellationToken));
        }, "Contrato inválido");
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await loansService.ListAsync(userId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.GetAsync(userId, id, cancellationToken));
        }, "Contrato inválido");
    }

    [HttpPost("simulate")]
    [HttpPost("simulations")]
    public async Task<IActionResult> Simulate([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.SimulateAsync(userId, request, cancellationToken));
        }, "Contrato inválido");
    }

    [HttpPost("simulations/compare")]
    public async Task<IActionResult> Compare([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.CompareAsync(userId, request, cancellationToken));
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
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            await loansService.DeleteAsync(userId, id, cancellationToken);
            return NoContent();
        }, "Exclusão inválida", invalidOperationTitle: "Exclusão não permitida", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.ArchiveAsync(userId, id, cancellationToken));
        }, "Arquivamento inválido");
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.CancelAsync(userId, id, cancellationToken));
        }, "Cancelamento inválido");
    }

    [HttpPost("{contractId:guid}/installments/{installmentId:guid}/pay")]
    public async Task<IActionResult> PayInstallment(Guid contractId, Guid installmentId, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loansService.PayInstallmentAsync(userId, contractId, installmentId, cancellationToken));
        }, "Pagamento inválido", invalidOperationTitle: "Parcela já paga", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }

}
