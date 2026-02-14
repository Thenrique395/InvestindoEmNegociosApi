using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class InvoiceImportController(IInvoiceImportService invoiceImportService) : ControllerBase
{
    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<InvoiceExtractResponse>> Extract([FromForm] UploadInvoiceRequest request, CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Envie um PDF válido.", Status = StatusCodes.Status400BadRequest });
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Formato não suportado. Use PDF.", Status = StatusCodes.Status400BadRequest });
        }

        await using var stream = file.OpenReadStream();
        var result = await invoiceImportService.ExtractAsync(stream, cancellationToken);
        return Ok(result);
    }
}
