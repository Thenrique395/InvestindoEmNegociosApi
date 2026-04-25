using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminRuntimeInfoService
{
    ScalabilityRuntimeDto Get();
}
