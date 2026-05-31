using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/cards")]
[Route("api/v1/cards")]
public class CardsController(ICardsService cardsService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsRead)]
    // Lists cards for the authenticated user.
    public async Task<IActionResult> List([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await cardsService.ListAsync(userId, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<CardResponse, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["createdAt"] = x => x.CreatedAt,
                ["updatedAt"] = x => x.UpdatedAt,
                ["nickname"] = x => x.Nickname,
                ["holderName"] = x => x.HolderName,
                ["creditLimit"] = x => x.CreditLimit,
                ["statementCloseDay"] = x => x.StatementCloseDay,
                ["dueDay"] = x => x.DueDay
            });

        if (isPaged)
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsCreateUpdate)]
    // Creates a new card (we store only last4, brand, and holder name).
    public async Task<IActionResult> Create([FromBody] CardRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var card = await cardsService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(List), card);
        }, "Invalid card");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsCreateUpdate)]
    // Updates card data owned by the current user.
    public async Task<IActionResult> Update(Guid id, [FromBody] CardRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var updated = await cardsService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }, "Invalid card");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsDelete)]
    // Deletes a card owned by the current user.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await cardsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Card", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

}
