using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/installments/{id:guid}/anticipations")]
[Route("api/v1/installments/{id:guid}/anticipations")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsAnticipate)]
public class InstallmentAnticipationsController(IInstallmentsService installmentsService) : AuthenticatedControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid id, [FromBody] AnticipationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var anticipated = await installmentsService.AnticipateAsync(userId, id, request, cancellationToken);
            if (!anticipated) return NotFound();
            return Ok();
        }, "Invalid installment", invalidOperationTitle: "Invalid installment", invalidOperationStatusCode: StatusCodes.Status400BadRequest);
    }
}
