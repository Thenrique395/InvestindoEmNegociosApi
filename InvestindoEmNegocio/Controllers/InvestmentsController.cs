using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using UglyToad.PdfPig.Core;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class InvestmentsController(
    IInvestmentsService investmentsService,
    IAuditService auditService,
    IB3ImportService b3ImportService,
    IB3SyncService b3SyncService,
    ILogger<InvestmentsController> logger) : ControllerBase
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

        if (isPaged)
        {
            ListQueryHelper.WritePaginationHeaders(Response, total, page, pageSize);
        }

        return Ok(items);
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
        try
        {
            var position = await investmentsService.CreatePositionAsync(userId, request, cancellationToken);
            return Ok(position);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Posição inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPut("positions/{id:guid}")]
    public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] CreateInvestmentPositionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var position = await investmentsService.UpdatePositionAsync(userId, id, request, cancellationToken);
            if (position is null) return NotFound();
            return Ok(position);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Posição inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpDelete("positions/{id:guid}")]
    public async Task<IActionResult> DeletePosition(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await investmentsService.DeletePositionAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await auditService.LogAsync(userId, "DELETE", "InvestmentPosition", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

    [HttpPost("positions/{id:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid id, [FromBody] CreateInvestmentMovementRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var movement = await investmentsService.AddMovementAsync(userId, id, request, cancellationToken);
            return Ok(movement);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Movimento inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPost("import/b3/extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<B3ExtractResponse>> ExtractB3([FromForm] UploadB3ReportRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Envie o relatório da B3 em PDF.", Status = StatusCodes.Status400BadRequest });
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Formato não suportado. Use PDF.", Status = StatusCodes.Status400BadRequest });
        }

        try
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var response = await b3ImportService.ExtractAsync(userId, stream, cancellationToken);
            return Ok(response);
        }
        catch (PdfDocumentFormatException ex)
        {
            logger.LogWarning(ex, "Falha ao ler relatorio B3 (PDF inválido).");
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Falha ao ler PDF",
                Detail = "O arquivo parece inválido ou protegido.",
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Relatório inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao extrair relatorio B3.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor.",
                Detail = "Nao foi possivel ler o relatório da B3.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    [HttpPost("import/b3/confirm")]
    public async Task<ActionResult<B3ConfirmImportResponse>> ConfirmB3([FromBody] ConfirmB3ImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var response = await b3ImportService.ConfirmAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Importação inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails { Title = "Acesso negado", Detail = ex.Message, Status = StatusCodes.Status401Unauthorized });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao confirmar importacao B3.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor.",
                Detail = "Nao foi possivel concluir a importação da B3.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
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
        try
        {
            var userId = GetUserId();
            var response = await b3SyncService.SyncAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Sincronização inválida", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao sincronizar B3.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor.",
                Detail = "Nao foi possivel sincronizar dados da B3.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
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
