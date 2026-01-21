using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class GoalsService(IGoalRepository goalRepository, ILogger<GoalsService> logger) : IGoalsService
{
    private readonly ILogger<GoalsService> _logger = logger;
    public async Task<IReadOnlyList<GoalResponse>> ListAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var data = await goalRepository.ListByUserAsync(userId, year, status, cancellationToken);
        return data.Select(ToResponse).ToList();
    }

    public async Task<GoalResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        return goal is null ? null : ToResponse(goal);
    }

    public async Task<GoalResponse> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = new Goal(userId, request.Title.Trim(), request.TargetAmount, request.Year, request.Description, GoalStatus.Planned, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate);
        await goalRepository.AddAsync(goal, cancellationToken);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal created {UserId} {GoalId}", userId, goal.Id);
        return ToResponse(goal);
    }

    public async Task<GoalResponse?> UpdateAsync(Guid userId, Guid id, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return null;

        goal.Update(request.Title.Trim(), request.TargetAmount, request.Year, request.Description, request.Status, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal updated {UserId} {GoalId}", userId, goal.Id);
        return ToResponse(goal);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return false;

        goalRepository.Remove(goal);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal deleted {UserId} {GoalId}", userId, goal.Id);
        return true;
    }

    private static void Validate(CreateGoalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Título é obrigatório.");
        if (request.TargetAmount <= 0) throw new ArgumentException("Valor da meta deve ser maior que zero.");
        if (request.Year < 2000 || request.Year > 2100) throw new ArgumentException("Ano inválido.");
    }

    private static GoalResponse ToResponse(Goal g) =>
        new(g.Id, g.Title, g.TargetAmount, g.CurrentAmount, g.Year, g.Description, g.Status, g.CreatedAt, g.UpdatedAt, g.ExpectedMonthly, g.TargetDate);
}
