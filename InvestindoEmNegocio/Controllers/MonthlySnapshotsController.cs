using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AppAuthorizationPolicies.AtLeastIntermediate)]
public class MonthlySnapshotsController(IMonthlyFinancialSnapshotService snapshotService) : ControllerBase
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

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
