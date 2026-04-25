using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILookupInstitutionService
{
    Task<IReadOnlyList<Institution>> GetInstitutionsAsync(InstitutionType? type = null, CancellationToken cancellationToken = default);
}
