using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingPortalService(
    IUserSubscriptionRepository userSubscriptionRepository,
    IStripeBillingGateway stripeBillingGateway,
    IOptions<StripeOptions> stripeOptions) : IBillingPortalService
{
    public async Task<BillingPortalSessionResponse> CreatePortalSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Assinatura não encontrada", "Nenhuma assinatura foi encontrada para este usuário.", StatusCodes.Status404NotFound);

        if (subscription.Provider == "mercado_pago")
        {
            // Mercado Pago não tem portal de cobrança hospedado — leva para a tela própria
            // de assinatura, que já tem cancelar/trocar de plano via API própria.
            var frontendBase = stripeOptions.Value.FrontendBaseUrl.TrimEnd('/');
            return new BillingPortalSessionResponse($"{frontendBase}{stripeOptions.Value.PortalReturnPath}");
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalCustomerId))
            throw new AppProblemException("Portal indisponível", "Esta assinatura ainda não possui um cliente externo vinculado.", StatusCodes.Status400BadRequest);

        var url = await stripeBillingGateway.CreatePortalSessionAsync(subscription.ExternalCustomerId, cancellationToken);
        return new BillingPortalSessionResponse(url);
    }
}
