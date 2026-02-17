using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IB3ImportService
{
    Task<B3ExtractResponse> ExtractAsync(Guid userId, Stream pdfStream, CancellationToken cancellationToken);
    Task<B3ConfirmImportResponse> ConfirmAsync(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken);
}
