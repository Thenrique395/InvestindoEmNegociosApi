using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/monthly-snapshots")]
[Route("api/v1/monthly-snapshots")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureMonthlySnapshotsAccess)]
public class MonthlyFinancialSnapshotsController(IMonthlyFinancialSnapshotService snapshotService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await snapshotService.ListAsync(userId, cancellationToken));
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateMonthlyFinancialSnapshotRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await snapshotService.GenerateAsync(userId, request.Year, request.Month, cancellationToken));
        }, "Snapshot inválido");
    }

}
