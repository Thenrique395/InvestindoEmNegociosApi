using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILookupsService
{
    Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Institution>> GetInstitutionsAsync(InstitutionType? type = null, CancellationToken cancellationToken = default);
}
