using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityService
{
    Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default);
}
