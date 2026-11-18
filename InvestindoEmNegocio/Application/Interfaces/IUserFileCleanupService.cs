namespace InvestindoEmNegocio.Application.Interfaces;

public interface IUserFileCleanupService
{
    Task DeleteUserFilesAsync(Guid userId, CancellationToken cancellationToken = default);
}
