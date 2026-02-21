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
public sealed class DataPortabilityController(
    IDataPortabilityFacadeService dataPortabilityFacadeService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        try
        {
            var (fileName, content) = await dataPortabilityFacadeService.ExportAsync(GetUserId(), cancellationToken);
            return File(content, "application/json", fileName);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<ImportUserDataResult>> Import([FromForm] ImportUserDataRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Envie um arquivo JSON para importação.", Status = StatusCodes.Status400BadRequest });
        }

        await using var stream = request.File.OpenReadStream();
        try
        {
            var result = await dataPortabilityFacadeService.ImportAsync(
                GetUserId(),
                stream,
                request.File.Length,
                request.ReplaceExisting,
                cancellationToken);
            return Ok(result);
        }
        catch (AppProblemException ex)
        {
            return Problem(ex.Detail, statusCode: ex.StatusCode, title: ex.Title);
        }
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
