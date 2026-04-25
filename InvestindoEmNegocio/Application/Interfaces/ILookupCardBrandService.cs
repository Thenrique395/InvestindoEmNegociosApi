using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILookupCardBrandService
{
    Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default);
}
