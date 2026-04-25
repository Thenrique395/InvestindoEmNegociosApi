using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBillingCheckoutCommandService
{
    Task<StartBillingCheckoutResponse> StartCheckoutAsync(Guid userId, StartBillingCheckoutRequest request, CancellationToken cancellationToken = default);
}
