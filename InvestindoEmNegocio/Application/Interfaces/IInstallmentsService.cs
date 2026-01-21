using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInstallmentsService
{
    Task<IReadOnlyList<InstallmentResponse>> ListAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default);
    Task<bool> PayAsync(Guid userId, Guid installmentId, PaymentRequest request, CancellationToken cancellationToken = default);
    Task<bool> AnticipateAsync(Guid userId, Guid installmentId, AnticipationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default);
}
