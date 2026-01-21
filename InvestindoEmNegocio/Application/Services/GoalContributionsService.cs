using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class GoalContributionsService(
    IGoalRepository goalRepository,
    IGoalContributionRepository goalContributionRepository,
    ILogger<GoalContributionsService> logger)
    : IGoalContributionsService
{
    private readonly ILogger<GoalContributionsService> _logger = logger;
    public async Task<IReadOnlyList<GoalContributionResponse>?> ListAsync(Guid userId, Guid goalId, CancellationToken cancellationToken = default)
    {
        var exists = await goalRepository.ExistsAsync(goalId, userId, cancellationToken);
        if (!exists) return null;

        var items = await goalContributionRepository.ListByGoalAsync(goalId, userId, cancellationToken);
        return items.Select(ToResponse).ToList();
    }

    public async Task<GoalContributionResponse?> CreateAsync(Guid userId, Guid goalId, GoalContributionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0) throw new ArgumentException("Valor deve ser maior que zero.");

        var goal = await goalRepository.GetByIdAsync(goalId, userId, cancellationToken);
        if (goal is null) return null;
        if (goal.Status == GoalStatus.Canceled) throw new InvalidOperationException("Meta cancelada não aceita aportes.");

        var restante = goal.TargetAmount - goal.CurrentAmount;
        if (restante <= 0) throw new InvalidOperationException("Meta já atingiu o valor alvo.");
        if (request.Amount > restante) throw new InvalidOperationException("Valor do aporte excede o restante da meta.");

        var contrib = new GoalContribution(goalId, userId, request.Amount, request.Date, request.Note);
        await goalContributionRepository.AddAsync(contrib, cancellationToken);

        var novoAcumulado = goal.CurrentAmount + request.Amount;
        if (novoAcumulado >= goal.TargetAmount)
            goal.SetAmountAndStatus(goal.TargetAmount, GoalStatus.Completed);
        else
            goal.SetAmountAndStatus(novoAcumulado, GoalStatus.InProgress);

        await goalContributionRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal contribution created {UserId} {GoalId} {ContributionId}", userId, goalId, contrib.Id);
        return ToResponse(contrib);
    }

    private static GoalContributionResponse ToResponse(GoalContribution g) =>
        new(g.Id, g.Amount, g.Date, g.Note, g.CreatedAt);
}
