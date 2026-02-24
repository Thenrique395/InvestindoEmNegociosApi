using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class AccountsController(IAccountsService accountsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await accountsService.ListAsync(userId, cancellationToken);
        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var account = await accountsService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(List), account);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Conta inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var updated = await accountsService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Conta inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await accountsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> Balance(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var balance = await accountsService.GetBalanceAsync(userId, id, cancellationToken);
        if (balance is null) return NotFound();
        return Ok(balance);
    }

    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> Transactions(
        Guid id,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await accountsService.ListTransactionsAsync(userId, id, fromUtc, toUtc, cancellationToken);
        if (data is null) return NotFound();
        return Ok(data);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
