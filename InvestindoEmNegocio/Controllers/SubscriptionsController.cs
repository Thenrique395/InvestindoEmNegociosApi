using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Route("api/v1/subscriptions")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureSubscriptionsManage)]
public class SubscriptionsController(
    ISubscriptionCatalogService subscriptionCatalogService,
    ISubscriptionManagementService subscriptionManagementService) : AuthenticatedControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionCatalogService.GetCatalogAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("change")]
    public async Task<IActionResult> Change([FromBody] ChangeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await subscriptionManagementService.ChangeAsync(GetUserId(), request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionManagementService.CancelAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("refund")]
    public async Task<IActionResult> Refund(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionManagementService.RequestRefundAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("request-trial")]
    public async Task<IActionResult> RequestTrial(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionManagementService.RequestTrialAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("retry-payment")]
    public async Task<IActionResult> RetryPayment(CancellationToken cancellationToken = default)
    {
        await subscriptionManagementService.RetryPaymentAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

}
