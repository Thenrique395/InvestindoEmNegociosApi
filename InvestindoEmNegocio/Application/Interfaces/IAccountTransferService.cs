using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAccountTransferService
{
    Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default);
}
