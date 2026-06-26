namespace InvestindoEmNegocio.Application.Interfaces;

public interface IReceiptStorageService
{
    Task<string> SaveAsync(
        Guid userId,
        Stream content,
        string originalFileName,
        string contentType,
        string baseUrl,
        CancellationToken cancellationToken = default);
}
