using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/loans/{contractId:guid}/amortizations")]
[Route("api/v1/loans/{contractId:guid}/amortizations")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureLoansAccess)]
public class LoanAmortizationsController(ILoanAmortizationService amortizationService) : AuthenticatedControllerBase
{
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate(Guid contractId, [FromBody] LoanAmortizationRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await amortizationService.SimulateAsync(userId, contractId, request, cancellationToken));
        }, "Amortização inválida", invalidOperationTitle: "Amortização não permitida", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(
        Guid contractId,
        [FromBody] LoanAmortizationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var effective = request with
            {
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? request.IdempotencyKey : idempotencyKey
            };
            return Ok(await amortizationService.ConfirmAsync(userId, contractId, effective, cancellationToken));
        }, "Amortização inválida", invalidOperationTitle: "Amortização não permitida", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }
}
