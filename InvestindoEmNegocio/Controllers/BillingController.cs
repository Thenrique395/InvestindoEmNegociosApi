using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class BillingController(IBillingService billingService) : ControllerBase
{
    [HttpPost("checkout")]
    [Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
    public async Task<ActionResult<StartBillingCheckoutResponse>> StartCheckout([FromBody] StartBillingCheckoutRequest request, CancellationToken cancellationToken)
    {
        var response = await billingService.StartCheckoutAsync(GetUserId(), request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("checkout-status/{checkoutId:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
    public async Task<IActionResult> GetCheckoutStatus(Guid checkoutId, CancellationToken cancellationToken)
    {
        var response = await billingService.GetCheckoutStatusAsync(GetUserId(), checkoutId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("checkout-status/by-session/{sessionId}")]
    [Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
    public async Task<IActionResult> GetCheckoutStatusBySession(string sessionId, CancellationToken cancellationToken)
    {
        var response = await billingService.GetCheckoutStatusByProviderSessionAsync(GetUserId(), sessionId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("portal")]
    [Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
    public async Task<ActionResult<BillingPortalSessionResponse>> CreatePortalSession(CancellationToken cancellationToken)
    {
        var response = await billingService.CreatePortalSessionAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        await billingService.ProcessStripeWebhookAsync(payload, signature, cancellationToken);
        return Ok();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
