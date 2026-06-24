using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/billing/stripe/webhook")]
[Route("api/v1/billing/stripe/webhook")]
public class StripeWebhooksController(IStripeBillingWebhookService stripeBillingWebhookService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("stripe-webhook")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        await stripeBillingWebhookService.ProcessStripeWebhookAsync(payload, signature, cancellationToken);
        return Ok();
    }
}
