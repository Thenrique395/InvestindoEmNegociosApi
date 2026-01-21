using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class LookupsService : ILookupsService
{
    private readonly IPaymentMethodRepository _paymentMethodRepository;
    private readonly ICardBrandRepository _cardBrandRepository;

    public LookupsService(IPaymentMethodRepository paymentMethodRepository, ICardBrandRepository cardBrandRepository)
    {
        _paymentMethodRepository = paymentMethodRepository;
        _cardBrandRepository = cardBrandRepository;
    }

    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await _paymentMethodRepository.ListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default)
    {
        return await _cardBrandRepository.ListActiveAsync(cancellationToken);
    }
}
