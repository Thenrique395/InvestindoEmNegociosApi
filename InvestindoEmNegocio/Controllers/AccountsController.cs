using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsAccess)]
public class AccountsController(
    IAccountsService accountsService,
    IOfxImportService ofxImportService,
    ICsvImportService csvImportService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await accountsService.ListAsync(userId, cancellationToken);
        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccountRequest request, CancellationToken cancellationToken)
    {
        EnsureAccountManagementAllowed();
        try
        {
            var userId = GetUserId();
            var account = await accountsService.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(List), account);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Conta inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AccountRequest request, CancellationToken cancellationToken)
    {
        EnsureAccountManagementAllowed();
        try
        {
            var userId = GetUserId();
            var updated = await accountsService.UpdateAsync(userId, id, request, cancellationToken);
            if (updated is null) return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Conta inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        EnsureAccountManagementAllowed();
        var userId = GetUserId();
        var removed = await accountsService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> Balance(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var balance = await accountsService.GetBalanceAsync(userId, id, cancellationToken);
        if (balance is null) return NotFound();
        return Ok(balance);
    }

    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> Transactions(
        Guid id,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await accountsService.ListTransactionsAsync(userId, id, fromUtc, toUtc, cancellationToken);
        if (data is null) return NotFound();
        return Ok(data);
    }

    [HttpPost("transfers")]
    public async Task<IActionResult> Transfer([FromBody] AccountTransferRequest request, CancellationToken cancellationToken)
    {
        EnsureAccountManagementAllowed();
        try
        {
            var userId = GetUserId();
            var transfer = await accountsService.TransferAsync(userId, request, cancellationToken);
            if (transfer is null) return NotFound();
            return Ok(transfer);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Transferência inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

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

        try
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var result = await ofxImportService.ExtractAsync(userId, request.AccountId, stream, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Arquivo OFX inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Importação OFX rejeitada", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpPost("ofx/import")]
    public async Task<ActionResult<BankStatementImportResultResponse>> ImportOfx([FromBody] BankStatementImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var result = await ofxImportService.ImportAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Importação OFX inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Importação OFX rejeitada", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
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

        try
        {
            var userId = GetUserId();
            await using var stream = file.OpenReadStream();
            var result = await csvImportService.ExtractAsync(userId, request.AccountId, stream, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Arquivo CSV inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Importação CSV rejeitada", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpPost("csv/import")]
    public async Task<ActionResult<BankStatementImportResultResponse>> ImportCsv([FromBody] BankStatementImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetUserId();
            var result = await csvImportService.ImportAsync(userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Importação CSV inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppProblemException("Importação CSV rejeitada", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private void EnsureAccountManagementAllowed()
    {
        var roleRaw = User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleRaw, true, out var role))
            throw new UnauthorizedAccessException("Perfil não identificado.");

        if (role == UserRole.Basic)
            throw new AppProblemException(
                "Plano Basic",
                "No plano Basic a conta principal é gerenciada automaticamente. Faça upgrade para criar ou editar contas.",
                StatusCodes.Status403Forbidden);
    }
}
