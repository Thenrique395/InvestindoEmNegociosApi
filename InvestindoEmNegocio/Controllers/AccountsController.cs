using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts")]
[Route("api/v1/accounts")]
public class AccountsController(IAccountsService accountsService) : AuthenticatedControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await accountsService.ListAsync(userId, cancellationToken);
        return Ok(data);
    }

    [HttpPost]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsManage)]
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
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsManage)]
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
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await accountsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/balance")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsRead)]
    public async Task<IActionResult> Balance(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var balance = await accountsService.GetBalanceAsync(userId, id, cancellationToken);
        if (balance is null) return NotFound();
        return Ok(balance);
    }

}
