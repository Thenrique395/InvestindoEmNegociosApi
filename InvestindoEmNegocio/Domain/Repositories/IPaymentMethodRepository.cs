using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IPaymentMethodRepository
{
    Task<List<PaymentMethod>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<List<PaymentMethod>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<PaymentMethod?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentMethod method, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
