namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAvatarStorageService
{
    Task<string> SaveAsync(
        Guid userId,
        Stream content,
        string originalFileName,
        string contentType,
        string baseUrl,
        CancellationToken cancellationToken = default);
}
