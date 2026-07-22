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
    // Lists default categories plus user categories, with an optional money type filter.
    // includeInactive surfaces the user's deactivated categories (management screen / history).
    public async Task<IActionResult> List([FromQuery] MoneyType? appliesTo, [FromQuery] bool includeInactive, [FromQuery] ListQuery query, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await categoriesService.ListAsync(userId, appliesTo, includeInactive, cancellationToken);
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
    // Creates a category owned by the current user.
    public async Task<IActionResult> Create([FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var created = await categoriesService.CreateAsync(userId, request, cancellationToken);
            return Created("", created);
        }, "Categoria inválida", invalidOperationTitle: "Conflito de categoria");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
    // Updates a user-owned category without touching default categories.
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var updated = await categoriesService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }, "Categoria inválida", invalidOperationTitle: "Conflito de categoria");
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
    // Activates or deactivates a user-owned category (soft state), keeping history intact.
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateCategoryStatusRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var updated = await categoriesService.SetStatusAsync(userId, id, request.IsActive, cancellationToken);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureCategoriesManage)]
    // Removes an unused user-owned category, or deactivates it when referenced by plans so the
    // history is preserved. Default categories are never touched.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var outcome = await categoriesService.DeleteAsync(userId, id, cancellationToken);
        if (outcome == CategoryDeletionOutcome.NotFound) return NotFound();

        var action = outcome == CategoryDeletionOutcome.Deactivated ? "deactivated" : "deleted";
        await auditService.LogAsync(userId, action == "deactivated" ? "DEACTIVATE" : "DELETE", "Category", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return Ok(new { action });
    }

}
