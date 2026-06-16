using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts/{id:guid}/transactions")]
[Route("api/v1/accounts/{id:guid}/transactions")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsRead)]
public class AccountTransactionsController(IAccountTransactionQueryService accountTransactionQueryService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid id,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (page.HasValue || pageSize.HasValue)
        {
            var paged = await accountTransactionQueryService.ListTransactionsPagedAsync(
                userId, id, fromUtc, toUtc, page ?? 1, pageSize ?? 50, cancellationToken);
            if (paged is null) return NotFound();
            return Ok(paged);
        }

        var data = await accountTransactionQueryService.ListTransactionsAsync(userId, id, fromUtc, toUtc, cancellationToken);
        if (data is null) return NotFound();
        return Ok(data);
    }
}
