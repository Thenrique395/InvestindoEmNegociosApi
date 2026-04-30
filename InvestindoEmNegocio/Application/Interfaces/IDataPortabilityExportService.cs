namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityExportService
{
    Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}
