using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UglyToad.PdfPig.Core;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Route("api/invoice-import")]
[Route("api/v1/invoice-import")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInvoiceImportAccess)]
public sealed class InvoiceImportController(IInvoiceImportService invoiceImportService, ILogger<InvoiceImportController> logger) : ControllerBase
{
    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<InvoiceExtractResponse>> Extract([FromForm] UploadInvoiceRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie um PDF válido.",
                StatusCodes.Status400BadRequest);

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use PDF.",
                StatusCodes.Status400BadRequest);

        try
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var result = await invoiceImportService.ExtractAsync(userId, stream, cancellationToken);
            return Ok(result);
        }
        catch (PdfDocumentFormatException ex)
        {
            logger.LogWarning(ex, "Falha ao ler PDF (formato inválido).");
            throw new AppProblemException(
                "Falha ao ler PDF",
                "O PDF parece inválido ou protegido.",
                StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar fatura.");
            throw new AppProblemException(
                "Erro interno do servidor.",
                "Nao foi possivel processar o PDF.",
                StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<InvoiceImportResultResponse>> Import([FromBody] InvoiceImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var result = await invoiceImportService.ImportAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException(
                "Importação inválida",
                ex.Message,
                StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException(
                "Importação rejeitada",
                ex.Message,
                StatusCodes.Status422UnprocessableEntity);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
