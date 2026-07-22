using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/installments")]
[Route("api/v1/installments")]
public class InstallmentsController(IInstallmentsService installmentsService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsRead)]
    // Lists user installments with optional status, due date, and money type filters.
    public async Task<IActionResult> List([FromQuery] InstallmentStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] MoneyType? type, [FromQuery] ListQuery query, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await installmentsService.ListAsync(userId, status, from, to, type, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<InstallmentResponse, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["dueDate"] = x => x.DueDate,
                ["amount"] = x => x.Amount,
                ["installmentNo"] = x => x.InstallmentNo,
                ["status"] = x => x.Status
            });

        if (isPaged)
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(items);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsManage)]
    // Updates amount and due date of a single installment in-place, preserving payments.
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInstallmentRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var updated = await installmentsService.UpdateAsync(userId, id, request, cancellationToken);
            if (!updated) return NotFound();
            await auditService.LogAsync(userId, "UPDATE", "Installment", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
            return NoContent();
        }, "Parcela inválida", unauthorizedAccessTitle: "Acesso negado");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsManage)]
    // Deletes a single installment and its payments without removing the parent plan.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var removed = await installmentsService.DeleteAsync(userId, id, cancellationToken);
            if (!removed) return NotFound();
            await auditService.LogAsync(userId, "DELETE", "Installment", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
            return NoContent();
        }, "Parcela inválida", unauthorizedAccessTitle: "Acesso negado");
    }

}
