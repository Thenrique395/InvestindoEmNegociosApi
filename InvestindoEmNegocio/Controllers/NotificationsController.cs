using System.Security.Claims;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController(INotificationsService notificationsService) : ControllerBase
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

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
