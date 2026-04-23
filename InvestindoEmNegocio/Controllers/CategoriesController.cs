using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/categories")]
[Route("api/v1/categories")]
public class CategoriesController(ICategoriesService categoriesService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesRead)]
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
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
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
            throw new AppProblemException("Categoria inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Conflito de categoria", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
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
            throw new AppProblemException("Categoria inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Conflito de categoria", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
    // Remove apenas categorias do próprio usuário (não remove categorias padrão).
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var removed = await categoriesService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Category", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

}
