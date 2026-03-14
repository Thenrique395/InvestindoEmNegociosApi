using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvoiceImportService
{
    Task<InvoiceExtractResponse> ExtractAsync(Guid userId, Stream pdfStream, CancellationToken cancellationToken);
    Task<InvoiceReconciliationResponse> ReconcileAsync(Guid userId, InvoiceImportRequest request, CancellationToken cancellationToken);
    Task<InvoiceImportResultResponse> ImportAsync(Guid userId, InvoiceImportRequest request, CancellationToken cancellationToken);
}
