using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityApplicationService
{
    Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ImportUserDataResult> ImportAsync(
        Guid userId,
        Stream stream,
        long fileLength,
        bool replaceExisting,
        CancellationToken cancellationToken = default);
}
