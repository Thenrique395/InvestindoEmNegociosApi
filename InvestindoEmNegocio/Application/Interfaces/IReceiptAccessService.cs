namespace InvestindoEmNegocio.Application.Interfaces;

public interface IReceiptAccessService
{
    Task<ReceiptFile?> OpenAsync(Guid userId, string fileName, CancellationToken cancellationToken = default);
}
