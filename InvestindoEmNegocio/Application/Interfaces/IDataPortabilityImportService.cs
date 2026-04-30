using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityImportService
{
    Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default);
}
