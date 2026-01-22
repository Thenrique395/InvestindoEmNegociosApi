using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class CardsController(ICardsService cardsService, IAuditService auditService) : ControllerBase
{
    [HttpGet]
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
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(items);
    }

    [HttpPost]
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
            return BadRequest(new ProblemDetails { Title = "Cartão inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("{id:guid}")]
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
            return BadRequest(new ProblemDetails { Title = "Cartão inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpDelete("{id:guid}")]
    // Remove cartão do usuário.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await cardsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Card", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

    [HttpGet("debt/total")]
    // Retorna o total da dívida em cartões do usuário.
    public async Task<IActionResult> GetTotalDebt(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var total = await cardsService.GetTotalDebtAsync(userId, cancellationToken);
        return Ok(new { total });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
