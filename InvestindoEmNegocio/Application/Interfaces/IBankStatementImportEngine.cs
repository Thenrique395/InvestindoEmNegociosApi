using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBankStatementImportEngine
{
    Task<IReadOnlyList<BankStatementPreviewItemDto>> BuildPreviewAsync(
        Guid userId,
        Guid? accountId,
        IReadOnlyList<BankStatementImportItemDto> items,
        CancellationToken cancellationToken);

    Task<BankStatementImportResultResponse> ImportAsync(
        Guid userId,
        BankStatementImportRequest request,
        CancellationToken cancellationToken);
}
