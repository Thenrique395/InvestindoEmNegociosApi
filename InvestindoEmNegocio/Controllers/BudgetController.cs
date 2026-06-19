using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/budget")]
[Route("api/v1/budget")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureBudgetAccess)]
public class BudgetController(IBudgetService budgetService) : AuthenticatedControllerBase
{
    [HttpGet("{year:int}/{month:int}")]
    public async Task<IActionResult> Get(int year, int month, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await budgetService.GetAsync(userId, year, month, cancellationToken));
    }

    [HttpPut("{year:int}/{month:int}/items")]
    public async Task<IActionResult> UpsertItems(int year, int month, [FromBody] List<BudgetItemRequest> items, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await budgetService.UpsertItemsAsync(userId, year, month, items, cancellationToken));
        }, "Dados de orçamento inválidos");
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            await budgetService.DeleteItemAsync(userId, itemId, cancellationToken);
            return NoContent();
        }, "Item não encontrado");
    }
}
