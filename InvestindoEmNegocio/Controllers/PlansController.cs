using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/plans")]
[Route("api/v1/plans")]
[Authorize(Policy = AppAuthorizationPolicies.FeaturePlansManage)]
public class PlansController(IPlansService plansService, IPlanHistoryService planHistoryService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpPost]
    // Creates an income or expense plan and generates installments based on the schedule type.
    public async Task<ActionResult<PlanResponse>> Create([FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync<PlanResponse>(async () =>
        {
            var plan = await plansService.CreateAsync(userId, request, cancellationToken);
            return Ok(plan);
        }, "Plano inválido");
    }

    [HttpGet]
    // Lists user plans with an optional income or expense type filter.
    public async Task<IActionResult> List([FromQuery] MoneyType? type, [FromQuery] ListQuery query, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await plansService.ListAsync(userId, type, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<PlanResponse, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["startDate"] = x => x.StartDate,
                ["amount"] = x => x.Amount,
                ["title"] = x => x.Title,
                ["type"] = x => x.Type,
                ["schedule"] = x => x.Schedule,
                ["status"] = x => x.Status
            });

        if (isPaged)
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    // Returns a single plan with its generated installments.
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await plansService.GetByIdAsync(userId, id, cancellationToken);
        if (data is null) return NotFound();
        return Ok(data);
    }

    [HttpPut("{id:guid}")]
    // Updates a plan and regenerates installments based on the schedule type.
    public async Task<ActionResult<PlanResponse>> Update(Guid id, [FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return await ExecuteWithProblemMappingAsync<PlanResponse>(async () =>
        {
            var plan = await plansService.UpdateAsync(userId, id, request, cancellationToken);
            if (plan is null) return NotFound();
            return Ok(plan);
        }, "Plano inválido");
    }

    [HttpGet("{id:guid}/history")]
    // Histórico do lançamento: parcelas e eventos, para a gaveta de Despesas/Receitas.
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var history = await planHistoryService.GetAsync(userId, id, cancellationToken);
        if (history is null) return NotFound();
        return Ok(history);
    }

    [HttpDelete("{id:guid}")]
    // Deletes a plan and cascades installment and payment removal.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await plansService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "Plan", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }
}
