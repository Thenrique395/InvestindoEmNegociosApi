using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/notifications")]
[Route("api/v1/notifications")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureNotificationsAccess)]
public class NotificationsController(INotificationsService notificationsService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int? limit = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var items = await notificationsService.ListAsync(userId, unreadOnly, limit, cancellationToken);
        return Ok(items);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var count = await notificationsService.GenerateAsync(userId, cancellationToken);
        return Ok(new { created = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await notificationsService.MarkAsReadAsync(userId, id, cancellationToken);
        return NoContent();
    }

}
