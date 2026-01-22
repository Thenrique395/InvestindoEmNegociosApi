using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class CategoriesController(ICategoriesService categoriesService, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    // Lista categorias padrão (UserId nulo) + do usuário. Pode filtrar por tipo (receita/despesa).
    public async Task<IActionResult> List([FromQuery] MoneyType? appliesTo, [FromQuery] ListQuery query, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await categoriesService.ListAsync(userId, appliesTo, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<CategoryResponse, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = x => x.Name,
                ["appliesTo"] = x => x.AppliesTo,
                ["isDefault"] = x => x.IsDefault
            });

        if (isPaged)
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(items);
    }

    [HttpPost]
    // Cria categoria exclusiva do usuário.
    public async Task<IActionResult> Create([FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var created = await categoriesService.CreateAsync(userId, request, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Categoria inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    // Atualiza categoria do usuário (não altera categorias padrão).
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var updated = await categoriesService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Categoria inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    // Remove apenas categorias do próprio usuário (não remove categorias padrão).
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var removed = await categoriesService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Category", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
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
