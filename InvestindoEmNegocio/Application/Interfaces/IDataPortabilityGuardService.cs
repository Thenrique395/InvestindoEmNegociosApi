namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityGuardService
{
    void EnsureEnabled();
    void ValidateImportFile(Guid userId, long fileLength);
}
