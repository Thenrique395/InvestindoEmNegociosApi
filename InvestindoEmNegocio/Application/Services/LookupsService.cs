using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class LookupsService(
    IPaymentMethodRepository paymentMethodRepository,
    ICardBrandRepository cardBrandRepository,
    IInstitutionRepository institutionRepository)
    : ILookupsService
{
    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await paymentMethodRepository.ListActiveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default)
    {
        return await cardBrandRepository.ListActiveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync(InstitutionType? type = null, CancellationToken cancellationToken = default)
    {
        return await institutionRepository.ListActiveAsync(type, cancellationToken);
    }
}
