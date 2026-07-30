using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILoanPaymentService
{
    Task<LoanPaymentResult> PayAsync(Guid userId, Guid contractId, Guid installmentId, LoanPaymentRequest request, CancellationToken cancellationToken = default);
    Task<LoanPaymentResult> ReverseAsync(Guid userId, Guid contractId, Guid installmentId, Guid paymentId, LoanPaymentReversalRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanPaymentHistoryItem>> ListByInstallmentAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default);
    Task<string?> AttachReceiptAsync(Guid userId, Guid installmentId, Guid paymentId, Stream content, string originalFileName, string contentType, string baseUrl, CancellationToken cancellationToken = default);
}
