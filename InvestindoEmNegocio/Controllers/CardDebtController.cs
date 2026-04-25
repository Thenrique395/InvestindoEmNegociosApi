using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/cards/debt")]
[Route("api/v1/cards/debt")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureCardsRead)]
public class CardDebtController(ICardsService cardsService) : AuthenticatedControllerBase
{
    [HttpGet("total")]
    public async Task<IActionResult> GetTotal(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var total = await cardsService.GetTotalDebtAsync(userId, cancellationToken);
        return Ok(new { total });
    }
}
