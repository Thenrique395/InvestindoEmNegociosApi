using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/investments")]
[Route("api/v1/investments")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvestmentsAccess)]
public class InvestmentB3Controller(
    IInvestmentsApplicationService investmentsApplicationService,
    IB3SyncService b3SyncService) : AuthenticatedControllerBase
{
    [HttpPost("import/b3/extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<B3ExtractResponse>> Extract([FromForm] UploadB3ReportRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie o relatório da B3 em PDF.",
                StatusCodes.Status400BadRequest);

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use PDF.",
                StatusCodes.Status400BadRequest);

        var userId = GetUserId();
        await using var stream = file.OpenReadStream();
        var response = await investmentsApplicationService.ExtractB3Async(userId, stream, cancellationToken);
        return Ok(response);
    }

    [HttpPost("import/b3/confirm")]
    public async Task<ActionResult<B3ConfirmImportResponse>> Confirm([FromBody] ConfirmB3ImportRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await investmentsApplicationService.ConfirmB3Async(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("b3/consent")]
    public async Task<ActionResult<B3ConsentStatusResponse>> GetConsent(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await b3SyncService.GetConsentStatusAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("b3/consent/mock-grant")]
    public async Task<ActionResult<B3ConsentStatusResponse>> GrantConsentMock(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await b3SyncService.GrantMockConsentAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("b3/sync")]
    public async Task<ActionResult<B3SyncResponse>> Sync([FromBody] B3SyncRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await investmentsApplicationService.SyncB3Async(userId, request, cancellationToken);
        return Ok(response);
    }
}
