using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InvestmentPlanningService(
    IInvestmentGoalRepository goalRepository,
    IInvestmentAllocationTargetRepository allocationTargetRepository,
    ILogger<InvestmentPlanningService> logger) :
    IInvestmentGoalQueryService,
    IInvestmentGoalCommandService,
    IInvestmentAllocationQueryService,
    IInvestmentAllocationCommandService
{
    private readonly ILogger<InvestmentPlanningService> _logger = logger;

    public async Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByUserAsync(userId, cancellationToken);
        return goal is null ? null : new InvestmentGoalDto(goal.Id, goal.TargetAmount);
    }

    public async Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await goalRepository.GetByUserAsync(userId, cancellationToken);
        if (existing is null)
        {
            var goal = new InvestmentGoal(userId, request.TargetAmount);
            await goalRepository.AddAsync(goal, cancellationToken);
            await goalRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Investment goal created {UserId} {GoalId}", userId, goal.Id);
            return new InvestmentGoalDto(goal.Id, goal.TargetAmount);
        }

        existing.SetTargetAmount(request.TargetAmount);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment goal updated {UserId} {GoalId}", userId, existing.Id);
        return new InvestmentGoalDto(existing.Id, existing.TargetAmount);
    }

    public async Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var current = await allocationTargetRepository.GetByUserAsync(userId, cancellationToken);
        if (current is null)
            // Ponto de partida editável pelo usuário — não é recomendação de investimento.
            return InvestmentsShared.MapAllocation(30m, 30m, 30m, 10m);

        return InvestmentsShared.MapAllocation(current.Rf, current.Acoes, current.Fundos, current.Cripto);
    }

    public async Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default)
    {
        InvestmentsShared.ValidateAllocation(request);

        var current = await allocationTargetRepository.GetByUserAsync(userId, cancellationToken);
        if (current is null)
        {
            var target = new InvestmentAllocationTarget(userId, request.Rf, request.Acoes, request.Fundos, request.Cripto);
            await allocationTargetRepository.AddAsync(target, cancellationToken);
            await allocationTargetRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Investment allocation target created {UserId} {TargetId}", userId, target.Id);
            return InvestmentsShared.MapAllocation(target.Rf, target.Acoes, target.Fundos, target.Cripto);
        }

        current.SetAllocation(request.Rf, request.Acoes, request.Fundos, request.Cripto);
        await allocationTargetRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment allocation target updated {UserId} {TargetId}", userId, current.Id);
        return InvestmentsShared.MapAllocation(current.Rf, current.Acoes, current.Fundos, current.Cripto);
    }
}
