using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/monthlysnapshots")]
[Route("api/v1/monthlysnapshots")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureMonthlySnapshotsAccess)]
public class MonthlySnapshotsController(IMonthlyFinancialSnapshotService snapshotService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await snapshotService.ListAsync(userId, cancellationToken));
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateMonthlySnapshotRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await snapshotService.GenerateAsync(userId, request.Year, request.Month, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Snapshot inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

}
