using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminPaymentMethodsService
{
    Task<IReadOnlyList<PaymentMethodAdminResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<PaymentMethodAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    Task<PaymentMethodAdminResponse> CreateAsync(string name, CancellationToken cancellationToken = default);
}
