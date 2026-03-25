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
public class LoansController(ILoansService loansService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await loansService.ListAsync(userId, cancellationToken));
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await loansService.SimulateAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Empréstimo inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LoanContractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await loansService.CreateAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Empréstimo inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
