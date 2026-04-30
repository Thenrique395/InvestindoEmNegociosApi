using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAccountsService :
    IAccountQueryService,
    IAccountCommandService,
    IAccountTransactionQueryService,
    IAccountTransferService
{
}
