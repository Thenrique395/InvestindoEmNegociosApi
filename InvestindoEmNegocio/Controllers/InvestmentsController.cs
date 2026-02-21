using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class InvestmentsController(
    IInvestmentsService investmentsService,
    IInvestmentsFacadeService investmentsFacadeService,
    IInvestmentBenchmarksService benchmarksService,
    IB3SyncService b3SyncService) : ControllerBase
{
    [HttpGet("goal")]
    public async Task<ActionResult<InvestmentGoalDto>> GetGoal(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await investmentsService.GetGoalAsync(userId, cancellationToken);
        if (goal is null) return NoContent();
        return Ok(goal);
    }

    [HttpPut("goal")]
    public async Task<ActionResult<InvestmentGoalDto>> UpsertGoal([FromBody] UpsertInvestmentGoalRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var goal = await investmentsService.UpsertGoalAsync(userId, request, cancellationToken);
        return Ok(goal);
    }

    [HttpGet("allocation-target")]
    public async Task<ActionResult<InvestmentAllocationTargetDto>> GetAllocationTarget(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var target = await investmentsService.GetAllocationTargetAsync(userId, cancellationToken);
        return Ok(target);
    }

    [HttpPut("allocation-target")]
    public async Task<ActionResult<InvestmentAllocationTargetDto>> UpsertAllocationTarget([FromBody] UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var target = await investmentsFacadeService.UpsertAllocationTargetAsync(userId, request, cancellationToken);
        return Ok(target);
    }

    [HttpGet("positions")]
    public async Task<IActionResult> ListPositions([FromQuery] ListQuery query, CancellationToken cancellationToken)
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
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(list);
    }

    [HttpGet("positions/{id:guid}")]
    public async Task<IActionResult> GetPosition(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsService.GetPositionAsync(userId, id, cancellationToken);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [HttpPost("positions")]
    public async Task<IActionResult> CreatePosition([FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsFacadeService.CreatePositionAsync(userId, request, cancellationToken);
        return Ok(position);
    }

    [HttpPut("positions/{id:guid}")]
    public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var position = await investmentsFacadeService.UpdatePositionAsync(userId, id, request, cancellationToken);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [HttpDelete("positions/{id:guid}")]
    public async Task<IActionResult> DeletePosition(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await investmentsFacadeService.DeletePositionAsync(userId, id, GetIpAddress(), GetUserAgent(), cancellationToken);
        return NoContent();
    }

    [HttpPost("positions/{id:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid id, [FromBody] CreateInvestmentMovementRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var movement = await investmentsFacadeService.AddMovementAsync(userId, id, request, cancellationToken);
        return Ok(movement);
    }

    [HttpGet("benchmarks")]
    public async Task<ActionResult<InvestmentBenchmarksResponse>> GetBenchmarks([FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        var response = await benchmarksService.GetBenchmarksAsync(months, cancellationToken);
        return Ok(response);
    }

    [HttpGet("market/quote")]
    public async Task<ActionResult<MarketQuoteResponse>> GetMarketQuote([FromQuery] string symbol, CancellationToken cancellationToken = default)
    {
        var response = await investmentsFacadeService.GetMarketQuoteAsync(symbol, cancellationToken);
        return Ok(response);
    }

    [HttpGet("market/profile")]
    public async Task<ActionResult<MarketProfileResponse>> GetMarketProfile([FromQuery] string symbol, CancellationToken cancellationToken = default)
    {
        var response = await investmentsFacadeService.GetMarketProfileAsync(symbol, cancellationToken);
        return Ok(response);
    }

    [HttpGet("market/history")]
    public async Task<ActionResult<MarketHistoryResponse>> GetMarketHistory([FromQuery] string symbol, [FromQuery] string period = "6mo", CancellationToken cancellationToken = default)
    {
        var response = await investmentsFacadeService.GetMarketHistoryAsync(symbol, period, cancellationToken);
        return Ok(response);
    }

    [HttpPost("import/b3/extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<B3ExtractResponse>> ExtractB3([FromForm] UploadB3ReportRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie o relatório da B3 em PDF.",
                StatusCodes.Status400BadRequest);
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use PDF.",
                StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId();
        await using var stream = file.OpenReadStream();
        var response = await investmentsFacadeService.ExtractB3Async(userId, stream, cancellationToken);
        return Ok(response);
    }

    [HttpPost("import/b3/confirm")]
    public async Task<ActionResult<B3ConfirmImportResponse>> ConfirmB3([FromBody] ConfirmB3ImportRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await investmentsFacadeService.ConfirmB3Async(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("b3/consent")]
    public async Task<ActionResult<B3ConsentStatusResponse>> GetB3Consent(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await b3SyncService.GetConsentStatusAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("b3/consent/mock-grant")]
    public async Task<ActionResult<B3ConsentStatusResponse>> GrantB3ConsentMock(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await b3SyncService.GrantMockConsentAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("b3/sync")]
    public async Task<ActionResult<B3SyncResponse>> SyncB3([FromBody] B3SyncRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await investmentsFacadeService.SyncB3Async(userId, request, cancellationToken);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }

}
