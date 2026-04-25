using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts")]
[Route("api/v1/accounts")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsImport)]
public class AccountImportsController(
    IOfxImportService ofxImportService,
    ICsvImportService csvImportService) : AuthenticatedControllerBase
{
    [HttpPost("ofx/extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<OfxExtractResponse>> ExtractOfx([FromForm] UploadOfxRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie um arquivo OFX válido.",
                StatusCodes.Status400BadRequest);

        var fileName = file.FileName ?? string.Empty;
        var contentType = file.ContentType ?? string.Empty;
        var isSupported = fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/x-ofx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase);
        if (!isSupported)
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use OFX.",
                StatusCodes.Status400BadRequest);

        return await ExecuteWithProblemMappingAsync<OfxExtractResponse>(async () =>
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var result = await ofxImportService.ExtractAsync(userId, request.AccountId, stream, cancellationToken);
            return Ok(result);
        }, "Invalid OFX file", invalidOperationTitle: "Rejected OFX import", invalidOperationStatusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpPost("ofx/import")]
    public async Task<ActionResult<BankStatementImportResultResponse>> ImportOfx([FromBody] BankStatementImportRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<BankStatementImportResultResponse>(async () =>
        {
            var userId = GetUserId();
            var result = await ofxImportService.ImportAsync(userId, request, cancellationToken);
            return Ok(result);
        }, "Invalid OFX import", invalidOperationTitle: "Rejected OFX import", invalidOperationStatusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpPost("csv/extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<CsvExtractResponse>> ExtractCsv([FromForm] UploadCsvStatementRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie um arquivo CSV válido.",
                StatusCodes.Status400BadRequest);

        var fileName = file.FileName ?? string.Empty;
        var isSupported = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.ContentType, "text/plain", StringComparison.OrdinalIgnoreCase);
        if (!isSupported)
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use CSV.",
                StatusCodes.Status400BadRequest);

        return await ExecuteWithProblemMappingAsync<CsvExtractResponse>(async () =>
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var result = await csvImportService.ExtractAsync(userId, request.AccountId, stream, cancellationToken);
            return Ok(result);
        }, "Invalid CSV file", invalidOperationTitle: "Rejected CSV import", invalidOperationStatusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpPost("csv/import")]
    public async Task<ActionResult<BankStatementImportResultResponse>> ImportCsv([FromBody] BankStatementImportRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync<BankStatementImportResultResponse>(async () =>
        {
            var userId = GetUserId();
            var result = await csvImportService.ImportAsync(userId, request, cancellationToken);
            return Ok(result);
        }, "Invalid CSV import", invalidOperationTitle: "Rejected CSV import", invalidOperationStatusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
