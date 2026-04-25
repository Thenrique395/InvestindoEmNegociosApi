using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/billing")]
[Route("api/v1/billing")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureBillingManage)]
public class BillingCheckoutsController(
    IBillingCheckoutCommandService billingCheckoutCommandService,
    IBillingCheckoutQueryService billingCheckoutQueryService) : AuthenticatedControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<StartBillingCheckoutResponse>> Start([FromBody] StartBillingCheckoutRequest request, CancellationToken cancellationToken)
    {
        var response = await billingCheckoutCommandService.StartCheckoutAsync(GetUserId(), request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("checkout-status/{checkoutId:guid}")]
    public async Task<IActionResult> GetStatus(Guid checkoutId, CancellationToken cancellationToken)
    {
        var response = await billingCheckoutQueryService.GetCheckoutStatusAsync(GetUserId(), checkoutId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("checkout-status/by-session/{sessionId}")]
    public async Task<IActionResult> GetStatusBySession(string sessionId, CancellationToken cancellationToken)
    {
        var response = await billingCheckoutQueryService.GetCheckoutStatusByProviderSessionAsync(GetUserId(), sessionId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
