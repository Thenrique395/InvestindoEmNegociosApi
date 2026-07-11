using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Progresso da meta a partir de lançamentos reais. Para metas recorrentes,
/// o progresso considera a OCORRÊNCIA (período) corrente; o alvo pode ser
/// sobrescrito por ocorrência (edição pontual).
/// </summary>
public sealed class GoalProgressService(IInvestDbContext db, IGoalRealizedReader reader) : IGoalProgressService
{
    public async Task<GoalProgressResponse?> GetProgressAsync(Guid userId, Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await db.Goals
            .Include(g => g.Scopes)
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var anchorStart = goal.StartDate ?? new DateOnly(goal.Year, 1, 1);
        var fallbackEnd = goal.EndDate ?? new DateOnly(goal.Year, 12, 31);

        DateOnly start, end;
        if (goal.Recurrence is RecurrenceType.None or RecurrenceType.Custom)
        {
            start = anchorStart;
            end = fallbackEnd;
        }
        else
        {
            var window = GoalPeriodCalculator.CurrentWindow(goal.Recurrence, anchorStart, fallbackEnd, today);
            start = window.Start;
            end = window.End;
        }

        var target = goal.TargetAmount;
        var occurrenceTarget = await db.GoalOccurrences
            .Where(o => o.GoalId == goal.Id && o.PeriodStart == start)
            .Select(o => (decimal?)o.TargetAmount)
            .FirstOrDefaultAsync(cancellationToken);
        if (occurrenceTarget.HasValue) target = occurrenceTarget.Value;

        var (realized, pending) = await reader.ReadAsync(goal, start, end, cancellationToken);

        var progress = GoalProgressCalculator.Calculate(
            goal.Kind, target, realized, pending, start, end, today, goal.WarningThreshold, goal.CriticalThreshold);

        return new GoalProgressResponse(
            goal.Id, goal.Kind, goal.Mode,
            progress.Target, progress.Realized, progress.Pending, progress.Percent, progress.Remaining,
            progress.Forecast, progress.DaysRemaining, progress.State.ToString(), start, end);
    }
}
