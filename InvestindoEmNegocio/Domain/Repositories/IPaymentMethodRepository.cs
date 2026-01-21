using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IPaymentMethodRepository
{
    Task<List<PaymentMethod>> ListAsync(CancellationToken cancellationToken = default);
}
