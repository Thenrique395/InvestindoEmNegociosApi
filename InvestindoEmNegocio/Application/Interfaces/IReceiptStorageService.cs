namespace InvestindoEmNegocio.Application.Interfaces;

public sealed record ReceiptFile(Stream Content, string ContentType);

public interface IReceiptStorageService
{
    Task<ReceiptFile?> OpenAsync(Guid userId, string fileName, CancellationToken cancellationToken = default);
    Task<string> SaveAsync(
        Guid userId,
        Stream content,
        string originalFileName,
        string contentType,
        string baseUrl,
        CancellationToken cancellationToken = default);
}
