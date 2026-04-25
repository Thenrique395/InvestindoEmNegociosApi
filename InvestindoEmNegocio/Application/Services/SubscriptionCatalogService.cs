using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionCatalogService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository) : ISubscriptionCatalogService
{
    public async Task<SubscriptionCatalogResponse> GetCatalogAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var current = SubscriptionResponseFactory.BuildCurrent(user, subscription);
        var plans = SubscriptionPlanCatalog.Plans
            .Select(plan => new SubscriptionPlanResponse(
                plan.Code,
                plan.Name,
                plan.Role.ToString(),
                plan.Description,
                plan.MonthlyPrice,
                plan.YearlyPrice,
                plan.Recommended,
                string.Equals(plan.Code, current.PlanCode, StringComparison.OrdinalIgnoreCase),
                plan.Features,
                plan.Limits))
            .ToList();

        return new SubscriptionCatalogResponse(current, plans, SubscriptionResponseFactory.Notes);
    }
}
