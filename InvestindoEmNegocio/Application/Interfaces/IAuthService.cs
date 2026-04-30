using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuthService :
    IAuthRegistrationService,
    IAuthAccessService,
    IAuthPasswordService
{
}
