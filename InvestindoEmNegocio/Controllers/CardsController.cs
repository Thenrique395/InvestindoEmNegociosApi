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
    // Lista cartões do usuário autenticado.
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
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsManage)]
    // Cria um novo cartão (armazenamos apenas last4 + marca + nome do titular).
    public async Task<IActionResult> Create([FromBody] CardRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var card = await cardsService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(List), card);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Cartão inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsManage)]
    // Atualiza dados do cartão do usuário.
    public async Task<IActionResult> Update(Guid id, [FromBody] CardRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var updated = await cardsService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Cartão inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCardsManage)]
    // Remove cartão do usuário.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await cardsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Card", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

}
