using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IDataPortabilityService :
    IDataPortabilityExportService,
    IDataPortabilityImportService
{
}
