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
[Authorize(Policy = AppAuthorizationPolicies.AtLeastBasic)]
public class SubscriptionsController(ISubscriptionsService subscriptionsService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionsService.GetCatalogAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("change")]
    public async Task<IActionResult> Change([FromBody] ChangeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await subscriptionsService.ChangeAsync(GetUserId(), request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken = default)
    {
        var response = await subscriptionsService.CancelAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
