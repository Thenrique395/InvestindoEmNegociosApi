using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/loans/{contractId:guid}/installments/{installmentId:guid}/payments")]
[Route("api/v1/loans/{contractId:guid}/installments/{installmentId:guid}/payments")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureLoansAccess)]
public class LoanPaymentsController(ILoanPaymentService loanPaymentService) : AuthenticatedControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid contractId, Guid installmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return Ok(await loanPaymentService.ListByInstallmentAsync(userId, installmentId, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Pay(
        Guid contractId,
        Guid installmentId,
        [FromBody] LoanPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            // O header Idempotency-Key tem prioridade; cai para o do corpo se ausente.
            var effective = request with
            {
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? request.IdempotencyKey : idempotencyKey
            };
            return Ok(await loanPaymentService.PayAsync(userId, contractId, installmentId, effective, cancellationToken));
        }, "Pagamento inválido", invalidOperationTitle: "Pagamento não permitido", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }

    [HttpPost("{paymentId:guid}/reverse")]
    public async Task<IActionResult> Reverse(
        Guid contractId,
        Guid installmentId,
        Guid paymentId,
        [FromBody] LoanPaymentReversalRequest? request,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            return Ok(await loanPaymentService.ReverseAsync(userId, contractId, installmentId, paymentId, request ?? new LoanPaymentReversalRequest(), cancellationToken));
        }, "Estorno inválido", invalidOperationTitle: "Estorno não permitido", invalidOperationStatusCode: StatusCodes.Status409Conflict);
    }

    [HttpPost("{paymentId:guid}/receipt")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AttachReceipt(Guid contractId, Guid installmentId, Guid paymentId, [FromForm] UploadReceiptRequest request, CancellationToken cancellationToken)
    {
        var receipt = request.Receipt;
        if (receipt is null || receipt.Length == 0)
            throw new AppProblemException("Arquivo inválido", "Envie um comprovante válido.", StatusCodes.Status400BadRequest);

        var userId = GetUserId();
        await using var stream = receipt.OpenReadStream();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var receiptUrl = await loanPaymentService.AttachReceiptAsync(
            userId, installmentId, paymentId, stream, receipt.FileName, receipt.ContentType, baseUrl, cancellationToken);

        if (receiptUrl is null) return NotFound();
        return Ok(new { receiptUrl });
    }
}
