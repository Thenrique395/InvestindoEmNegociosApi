using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts/{id:guid}/transactions")]
[Route("api/v1/accounts/{id:guid}/transactions")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsRead)]
public class AccountTransactionsController(IAccountsService accountsService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
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
}
