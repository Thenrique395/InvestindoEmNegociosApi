using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/installments/{id:guid}/payments")]
[Route("api/v1/installments/{id:guid}/payments")]
public class InstallmentPaymentsController(IInstallmentsService installmentsService) : AuthenticatedControllerBase
{
    [HttpPost]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsPay)]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var paid = await installmentsService.PayAsync(userId, id, request, cancellationToken);
        if (!paid) return NotFound();
        return Ok();
    }

    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsRead)]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var payments = await installmentsService.ListPaymentsAsync(userId, id, cancellationToken);
        if (payments is null) return NotFound();
        return Ok(payments);
    }

    [HttpPost("{paymentId:guid}/reversals")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsPay)]
    public async Task<IActionResult> Reverse(Guid id, Guid paymentId, [FromBody] PaymentReversalRequest? request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var reversed = await installmentsService.ReversePaymentAsync(userId, id, paymentId, request ?? new PaymentReversalRequest(), cancellationToken);
        if (!reversed) return NotFound();
        return Ok();
    }
}
