using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/cards/{id:guid}/statements")]
[Route("api/v1/cards/{id:guid}/statements")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureCardsStatements)]
public class CardStatementsController(ICardsService cardsService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid id,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var cycles = await cardsService.ListStatementCyclesAsync(userId, id, year, month, cancellationToken);
            if (cycles is null) return NotFound();
            return Ok(cycles);
        }, "Invalid statement filter");
    }
}
