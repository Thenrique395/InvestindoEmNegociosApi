using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/spaces")]
[Route("api/v1/spaces")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureSpacesManage)]
public class SpacesController(
    ISpaceService spaceService,
    IAuthCookieService authCookieService) : AuthenticatedControllerBase
{
    private const int RefreshTokenDays = 30;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await spaceService.ListAsync(userId, cancellationToken);
        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SpaceRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var space = await spaceService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(List), space);
        }, "Espaço inválido");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SpaceRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var updated = await spaceService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }, "Espaço inválido");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var removed = await spaceService.DeleteAsync(userId, id, cancellationToken);
            if (!removed) return NotFound();
            return NoContent();
        }, "Espaço inválido");
    }

    [HttpPost("{id:guid}/enter")]
    public async Task<IActionResult> Enter(Guid id, [FromBody] EnterSpaceRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var session = await spaceService.EnterAsync(userId, id, request, cancellationToken);
            authCookieService.SetAuthCookies(Response, session.Token, session.ExpiresAt, session.RefreshToken, DateTime.UtcNow.AddDays(RefreshTokenDays));
            authCookieService.SetCsrfCookie(Response);
            return Ok(new AuthSessionResponse(session.UserId, session.Name, session.Email, session.Role, session.ExpiresAt));
        }, "Espaço inválido", unauthorizedAccessTitle: "Senha inválida", unauthorizedAccessStatusCode: StatusCodes.Status401Unauthorized);
    }
}
