using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class LookupsService(IPaymentMethodRepository paymentMethodRepository, ICardBrandRepository cardBrandRepository)
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
}
