using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILookupsService
{
    Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default);
}
