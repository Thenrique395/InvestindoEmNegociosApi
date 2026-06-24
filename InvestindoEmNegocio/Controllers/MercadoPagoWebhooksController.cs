using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/billing/mercadopago/webhook")]
[Route("api/v1/billing/mercadopago/webhook")]
public class MercadoPagoWebhooksController(IMercadoPagoBillingWebhookService mercadoPagoBillingWebhookService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("mercadopago-webhook")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["x-signature"].ToString();
        var requestId = Request.Headers["x-request-id"].ToString();
        await mercadoPagoBillingWebhookService.ProcessWebhookAsync(payload, requestId, signature, cancellationToken);
        return Ok();
    }
}
