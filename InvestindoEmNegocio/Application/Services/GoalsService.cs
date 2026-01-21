using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class GoalsService : IGoalsService
{
    private readonly IGoalRepository _goalRepository;

    public GoalsService(IGoalRepository goalRepository)
    {
        _goalRepository = goalRepository;
    }

    public async Task<IReadOnlyList<GoalResponse>> ListAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var data = await _goalRepository.ListByUserAsync(userId, year, status, cancellationToken);
        return data.Select(ToResponse).ToList();
    }

    public async Task<GoalResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, userId, cancellationToken);
        return goal is null ? null : ToResponse(goal);
    }

    public async Task<GoalResponse> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = new Goal(userId, request.Title.Trim(), request.TargetAmount, request.Year, request.Description, GoalStatus.Planned, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate);
        await _goalRepository.AddAsync(goal, cancellationToken);
        await _goalRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(goal);
    }

    public async Task<GoalResponse?> UpdateAsync(Guid userId, Guid id, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = await _goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return null;

        goal.Update(request.Title.Trim(), request.TargetAmount, request.Year, request.Description, request.Status, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate);
        await _goalRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(goal);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return false;

        _goalRepository.Remove(goal);
        await _goalRepository.SaveChangesAsync(cancellationToken);
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
