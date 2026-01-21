using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(ICategoriesService categoriesService) : ControllerBase
{
    private readonly ICategoriesService _categoriesService = categoriesService;

    [HttpGet]
    // Lista categorias padrão (UserId nulo) + do usuário. Pode filtrar por tipo (receita/despesa).
    public async Task<IActionResult> List([FromQuery] MoneyType? appliesTo, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await _categoriesService.ListAsync(userId, appliesTo, cancellationToken);
        return Ok(data);
    }

    [HttpPost]
    // Cria categoria exclusiva do usuário.
    public async Task<IActionResult> Create([FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var created = await _categoriesService.CreateAsync(userId, request, cancellationToken);
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
            var updated = await _categoriesService.UpdateAsync(userId, id, request, cancellationToken);
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
        var removed = await _categoriesService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
