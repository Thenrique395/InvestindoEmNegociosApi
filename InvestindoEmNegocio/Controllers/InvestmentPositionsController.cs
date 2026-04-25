using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/investments/positions")]
[Route("api/v1/investments/positions")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvestmentsAccess)]
public class InvestmentPositionsController(
    IInvestmentsService investmentsService,
    IInvestmentsApplicationService investmentsApplicationService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await investmentsService.ListPositionsAsync(userId, cancellationToken);
        var (items, total, page, pageSize, isPaged) = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<InvestmentPositionDto, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["asset"] = x => x.Asset,
                ["type"] = x => x.Type,
                ["quantity"] = x => x.Quantity,
                ["avgPrice"] = x => x.AvgPrice,
                ["openedAt"] = x => x.OpenedAt,
                ["account"] = x => x.Account,
                ["category"] = x => x.Category
            });

        var list = items.ToList();
        list = await investmentsService.EnrichWithMarketAsync(list, cancellationToken);

        if (isPaged)
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsService.GetPositionAsync(userId, id, cancellationToken);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsApplicationService.CreatePositionAsync(userId, request, cancellationToken);
        return Ok(position);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsApplicationService.UpdatePositionAsync(userId, id, request, cancellationToken);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await investmentsApplicationService.DeletePositionAsync(userId, id, GetIpAddress(), GetUserAgent(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid id, [FromBody] CreateInvestmentMovementRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var movement = await investmentsApplicationService.AddMovementAsync(userId, id, request, cancellationToken);
        return Ok(movement);
    }
}
