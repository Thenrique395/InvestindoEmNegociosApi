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
        var (fileName, content) = await dataPortabilityFacadeService.ExportAsync(GetUserId(), cancellationToken);
        return File(content, "application/json", fileName);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<ImportUserDataResult>> Import([FromForm] ImportUserDataRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie um arquivo JSON para importação.",
                StatusCodes.Status400BadRequest);
        }

        await using var stream = request.File.OpenReadStream();
        var result = await dataPortabilityFacadeService.ImportAsync(
            GetUserId(),
            stream,
            request.File.Length,
            request.ReplaceExisting,
            cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (Guid.TryParse(claim, out var id))
            return id;

        throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
