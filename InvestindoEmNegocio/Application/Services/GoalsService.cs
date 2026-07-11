using System;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class GoalsService(IGoalRepository goalRepository, IGoalContributionRepository goalContributionRepository, ICurrentSpaceAccessor currentSpaceAccessor, IInvestDbContext db, ILogger<GoalsService> logger) : IGoalsService
{
    private readonly ILogger<GoalsService> _logger = logger;
    private const string IncomeGoalTitle = "Meta de Receita";
    public async Task<IReadOnlyList<GoalResponse>> ListAsync(Guid userId, int? year, GoalStatus? status, CancellationToken cancellationToken = default)
    {
        var data = await goalRepository.ListByUserAsync(userId, year, status, cancellationToken);
        return data.Select(CreateGoalResponse).ToList();
    }

    public async Task<GoalResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        return goal is null ? null : CreateGoalResponse(goal);
    }

    public async Task<GoalResponse?> GetIncomeGoalAsync(Guid userId, int year, CancellationToken cancellationToken = default)
    {
        var data = await goalRepository.ListByUserAsync(userId, year, null, cancellationToken);
        var goal = data.FirstOrDefault(g => string.Equals(g.Title, IncomeGoalTitle, StringComparison.OrdinalIgnoreCase));
        return goal is null ? null : CreateGoalResponse(goal);
    }

    public async Task<GoalResponse> UpsertIncomeGoalAsync(Guid userId, UpsertIncomeGoalRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ExpectedMonthly <= 0) throw new ArgumentException("Valor mensal deve ser maior que zero.");
        if (request.Year < 2000 || request.Year > 2100) throw new ArgumentException("Ano inválido.");

        var data = await goalRepository.ListByUserAsync(userId, request.Year, null, cancellationToken);
        var existing = data.FirstOrDefault(g => string.Equals(g.Title, IncomeGoalTitle, StringComparison.OrdinalIgnoreCase));
        var targetAmount = request.ExpectedMonthly * 12;

        if (existing is null)
        {
            var goal = new Goal(
                userId,
                currentSpaceAccessor.RequireSpaceId(),
                IncomeGoalTitle,
                targetAmount,
                request.Year,
                "Meta mensal de receita",
                GoalStatus.Planned,
                0,
                request.ExpectedMonthly,
                null,
                GoalKind.Income);
            await goalRepository.AddAsync(goal, cancellationToken);
            await goalRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Income goal created {UserId} {GoalId}", userId, goal.Id);
            return CreateGoalResponse(goal);
        }

        existing.Update(
            IncomeGoalTitle,
            targetAmount,
            request.Year,
            existing.Description,
            existing.Status,
            existing.CurrentAmount,
            request.ExpectedMonthly,
            existing.TargetDate);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Income goal updated {UserId} {GoalId}", userId, existing.Id);
        return CreateGoalResponse(existing);
    }

    public async Task<GoalResponse> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = new Goal(userId, currentSpaceAccessor.RequireSpaceId(), request.Title.Trim(), request.TargetAmount, request.Year, request.Description, GoalStatus.Planned, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate, request.Kind);
        await ApplyPlanningAsync(userId, goal, request, cancellationToken);
        await goalRepository.AddAsync(goal, cancellationToken);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal created {UserId} {GoalId}", userId, goal.Id);
        return CreateGoalResponse(goal);
    }

    public async Task<GoalResponse?> UpdateAsync(Guid userId, Guid id, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return null;

        goal.Update(request.Title.Trim(), request.TargetAmount, request.Year, request.Description, request.Status, request.CurrentAmount, request.ExpectedMonthly, request.TargetDate, request.Kind);
        await ApplyPlanningAsync(userId, goal, request, cancellationToken);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal updated {UserId} {GoalId}", userId, goal.Id);
        return CreateGoalResponse(goal);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByIdAsync(id, userId, cancellationToken);
        if (goal is null) return false;

        var now = DateTime.UtcNow;
        var contributions = await goalContributionRepository.ListByGoalAsync(goal.Id, userId, cancellationToken, track: true);
        foreach (var contribution in contributions)
            contribution.MarkDeleted(now);

        goal.MarkDeleted(now);
        await goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Goal deleted {UserId} {GoalId}", userId, goal.Id);
        return true;
    }

    // ---- Ciclo de vida ------------------------------------------------------

    public Task<GoalResponse?> PauseAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        TransitionAsync(userId, id, g => g.Pause(), ct);

    public Task<GoalResponse?> ResumeAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        TransitionAsync(userId, id, g => g.Resume(), ct);

    public Task<GoalResponse?> ArchiveAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        TransitionAsync(userId, id, g => g.Archive(DateTime.UtcNow), ct);

    public Task<GoalResponse?> CompleteAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        TransitionAsync(userId, id, g => g.CompleteManually(), ct);

    private async Task<GoalResponse?> TransitionAsync(Guid userId, Guid id, Action<Goal> transition, CancellationToken ct)
    {
        var goal = await goalRepository.GetByIdAsync(id, userId, ct);
        if (goal is null) return null;
        transition(goal);
        await goalRepository.SaveChangesAsync(ct);
        return CreateGoalResponse(goal);
    }

    // ---- Planejamento / escopo ---------------------------------------------

    private async Task ApplyPlanningAsync(Guid userId, Goal goal, CreateGoalRequest request, CancellationToken ct)
    {
        var mode = request.Mode ?? Goal.DefaultModeFor(request.Kind);
        goal.ConfigurePlanning(mode, request.StartDate, request.EndDate, request.Recurrence, request.WarningThreshold, request.CriticalThreshold);

        if (request.Scopes is null) return;
        var scopes = await BuildAndValidateScopesAsync(userId, goal.Id, request.Scopes, ct);
        goal.ReplaceScopes(scopes);
    }

    private async Task<List<GoalScope>> BuildAndValidateScopesAsync(Guid userId, Guid goalId, IReadOnlyList<GoalScopeDto> scopes, CancellationToken ct)
    {
        var result = new List<GoalScope>();
        foreach (var dto in scopes)
        {
            switch (dto.ScopeType)
            {
                case GoalScopeType.Category:
                    var categoryOk = await db.Categories.AsNoTracking()
                        .AnyAsync(c => c.Id == dto.RefId && (c.UserId == userId || c.UserId == null), ct);
                    if (!categoryOk) throw new ArgumentException("Categoria não encontrada ou não pertence ao usuário.");
                    break;
                case GoalScopeType.Account:
                    var accountOk = await db.Accounts.AsNoTracking()
                        .AnyAsync(a => a.Id == dto.RefId && a.UserId == userId, ct);
                    if (!accountOk) throw new ArgumentException("Conta não encontrada ou não pertence ao usuário.");
                    break;
                // Portfolio: preparado para o futuro; sem validação de posse nesta fase.
            }
            result.Add(new GoalScope(goalId, dto.ScopeType, dto.RefId));
        }
        return result;
    }

    private static void Validate(CreateGoalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Título é obrigatório.");
        if (request.TargetAmount <= 0) throw new ArgumentException("Valor da meta deve ser maior que zero.");
        if (request.Year < 2000 || request.Year > 2100) throw new ArgumentException("Ano inválido.");
    }

    private static GoalResponse CreateGoalResponse(Goal g) =>
        new(g.Id, g.Title, g.TargetAmount, g.CurrentAmount, g.Year, g.Description, g.Status, g.CreatedAt, g.UpdatedAt, g.ExpectedMonthly, g.TargetDate, g.Kind,
            g.Mode, g.StartDate, g.EndDate, g.Recurrence, g.WarningThreshold, g.CriticalThreshold, g.ArchivedAt,
            g.Scopes.Select(s => new GoalScopeDto(s.ScopeType, s.RefId)).ToList());
}
