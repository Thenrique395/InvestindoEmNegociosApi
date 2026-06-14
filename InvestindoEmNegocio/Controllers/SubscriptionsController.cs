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
    ISubscriptionManagementService subscriptionManagementService,
    IAuthCookieService authCookieService) : AuthenticatedControllerBase
{
    private const int RefreshTokenDays = 30;

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
        return Ok(ToApiResponse(response));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionManagementService.CancelAsync(GetUserId(), cancellationToken);
        return Ok(ToApiResponse(response));
    }

    private SubscriptionChangeApiResponse ToApiResponse(SubscriptionChangeResponse response)
    {
        var session = response.Session;
        authCookieService.SetAuthCookies(Response, session.Token, session.ExpiresAt, session.RefreshToken, DateTime.UtcNow.AddDays(RefreshTokenDays));
        authCookieService.SetCsrfCookie(Response);
        var apiSession = new AuthSessionResponse(session.UserId, session.Name, session.Email, session.Role, session.ExpiresAt);
        return new SubscriptionChangeApiResponse(response.Current, apiSession, response.Notes);
    }
}
