using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICsvImportService
{
    Task<CsvExtractResponse> ExtractAsync(Guid userId, Guid? accountId, Stream stream, CancellationToken cancellationToken);
    Task<BankStatementImportResultResponse> ImportAsync(Guid userId, BankStatementImportRequest request, CancellationToken cancellationToken);
}
