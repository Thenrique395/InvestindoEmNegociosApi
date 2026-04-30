using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityService(
    IDataPortabilityExportService dataPortabilityExportService,
    IDataPortabilityImportService dataPortabilityImportService) : IDataPortabilityService
{
    public Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dataPortabilityExportService.ExportAsync(userId, cancellationToken);

    public Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default) =>
        dataPortabilityImportService.ImportAsync(userId, stream, replaceExisting, cancellationToken);
}
