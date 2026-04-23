using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/dataportability")]
[Route("api/v1/dataportability")]
public sealed class DataPortabilityController(
    IDataPortabilityFacadeService dataPortabilityFacadeService) : AuthenticatedControllerBase
{
    [HttpGet("export")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureDataPortabilityExport)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var (fileName, content) = await dataPortabilityFacadeService.ExportAsync(GetUserId(), cancellationToken);
        return File(content, "application/json", fileName);
    }

    [HttpPost("import")]
    [Authorize(Policy = AppAuthorizationPolicies.FeatureDataPortabilityImport)]
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

}
