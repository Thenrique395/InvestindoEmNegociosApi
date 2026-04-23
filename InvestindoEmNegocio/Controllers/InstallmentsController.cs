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
[Route("api/installments")]
[Route("api/v1/installments")]
public class InstallmentsController(IInstallmentsService installmentsService, IAuditService auditService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsRead)]
    // Lista parcelas do usuário com filtros opcionais por status, vencimento e tipo (receita/despesa).
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

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsManage)]
    // Remove apenas a parcela (e pagamentos) sem excluir o plano inteiro.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var removed = await installmentsService.DeleteAsync(userId, id, cancellationToken);
            if (!removed) return NotFound();
            await auditService.LogAsync(userId, "DELETE", "Installment", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AppProblemException("Acesso negado", ex.Message, StatusCodes.Status403Forbidden);
        }
    }

}
