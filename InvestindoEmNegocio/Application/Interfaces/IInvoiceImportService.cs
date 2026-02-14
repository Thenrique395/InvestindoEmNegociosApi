using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvoiceImportService
{
    Task<InvoiceExtractResponse> ExtractAsync(Stream pdfStream, CancellationToken cancellationToken);
}
