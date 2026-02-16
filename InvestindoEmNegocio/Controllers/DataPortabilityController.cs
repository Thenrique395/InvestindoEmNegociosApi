using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class DataPortabilityController(
    IDataPortabilityService dataPortabilityService,
    IOptions<DataPortabilityOptions> options) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Funcionalidade desabilitada",
                Detail = "A exportação/importação de dados está desativada.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var (fileName, content) = await dataPortabilityService.ExportAsync(GetUserId(), cancellationToken);
        return File(content, "application/json", fileName);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<ImportUserDataResult>> Import([FromForm] ImportUserDataRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Funcionalidade desabilitada",
                Detail = "A exportação/importação de dados está desativada.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Arquivo inválido",
                Detail = "Envie um arquivo JSON para importação.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var maxBytes = Math.Max(1, options.Value.MaxImportSizeMb) * 1024L * 1024L;
        if (request.File.Length > maxBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Arquivo muito grande",
                Detail = $"Tamanho máximo permitido: {options.Value.MaxImportSizeMb} MB.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var stream = request.File.OpenReadStream();
        var result = await dataPortabilityService.ImportAsync(GetUserId(), stream, request.ReplaceExisting, cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }

        throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
