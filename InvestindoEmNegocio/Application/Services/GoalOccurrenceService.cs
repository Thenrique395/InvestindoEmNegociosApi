using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Recorrência de metas: gera as ocorrências (períodos) sob demanda até hoje,
/// preserva o histórico e apura o realizado de cada período. Metas de período
/// único têm exatamente uma ocorrência.
/// </summary>
public sealed class GoalOccurrenceService(IInvestDbContext db, IGoalRealizedReader reader) : IGoalOccurrenceService
{
    private const int MaxBackfill = 120;

    public async Task<IReadOnlyList<GoalOccurrenceResponse>?> EnsureAndListAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await db.Goals.Include(g => g.Scopes).FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, ct);
        if (goal is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureOccurrencesAsync(goal, today, ct);

        var occurrences = await db.GoalOccurrences
            .Where(o => o.GoalId == goal.Id)
            .OrderByDescending(o => o.Sequence)
            .ToListAsync(ct);

        var result = new List<GoalOccurrenceResponse>(occurrences.Count);
        foreach (var occ in occurrences)
        {
            var (realized, _) = await reader.ReadAsync(goal, occ.PeriodStart, occ.PeriodEnd, ct);
            var percent = occ.TargetAmount > 0 ? Math.Round(realized / occ.TargetAmount * 100m, 2) : 0m;
            result.Add(new GoalOccurrenceResponse(occ.Id, occ.Sequence, occ.PeriodStart, occ.PeriodEnd, occ.TargetAmount, realized, percent, occ.Status.ToString(), occ.Contains(today)));
        }
        return result;
    }

    public async Task<bool> OverrideCurrentTargetAsync(Guid userId, Guid goalId, decimal targetAmount, CancellationToken ct = default)
    {
        if (targetAmount <= 0) throw new ArgumentException("Valor da ocorrência deve ser maior que zero.");
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, ct);
        if (goal is null) return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureOccurrencesAsync(goal, today, ct);

        var current = await db.GoalOccurrences
            .Where(o => o.GoalId == goal.Id && o.PeriodStart <= today && o.PeriodEnd >= today)
            .OrderByDescending(o => o.Sequence)
            .FirstOrDefaultAsync(ct);
        if (current is null) return false;

        current.OverrideTarget(targetAmount);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureOccurrencesAsync(Goal goal, DateOnly today, CancellationToken ct)
    {
        var anchorStart = goal.StartDate ?? new DateOnly(goal.Year, 1, 1);
        var fallbackEnd = goal.EndDate ?? new DateOnly(goal.Year, 12, 31);

        var existing = await db.GoalOccurrences.Where(o => o.GoalId == goal.Id).ToListAsync(ct);

        if (goal.Recurrence is RecurrenceType.None or RecurrenceType.Custom)
        {
            if (existing.Count == 0)
            {
                db.GoalOccurrences.Add(new GoalOccurrence(goal.Id, 1, anchorStart, fallbackEnd, goal.TargetAmount));
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var currentSeq = GoalPeriodCalculator.CurrentSequence(goal.Recurrence, anchorStart, today);
        var maxSeq = existing.Count > 0 ? existing.Max(o => o.Sequence) : 0;
        var from = Math.Max(1, maxSeq + 1);
        if (currentSeq - from > MaxBackfill) from = currentSeq - MaxBackfill;

        var created = new List<GoalOccurrence>();
        for (var seq = from; seq <= currentSeq; seq++)
        {
            var window = GoalPeriodCalculator.WindowForSequence(goal.Recurrence, anchorStart, fallbackEnd, seq);
            var occ = new GoalOccurrence(goal.Id, seq, window.Start, window.End, goal.TargetAmount);
            db.GoalOccurrences.Add(occ);
            created.Add(occ);
        }

        // Ocorrências de períodos passados são encerradas (histórico).
        var now = DateTime.UtcNow;
        var closedAny = false;
        foreach (var occ in existing.Concat(created))
        {
            if (occ.Sequence < currentSeq && occ.Status != GoalOccurrenceStatus.Closed)
            {
                occ.Close(now);
                closedAny = true;
            }
        }

        if (created.Count > 0 || closedAny)
            await db.SaveChangesAsync(ct);
    }
}
