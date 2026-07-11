using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Goals;

/// <summary>Descritor de um alerta de meta, pronto para virar notificação.</summary>
public sealed record GoalAlertDescriptor(NotificationKind Kind, string Title, string Message, string ReferenceKey);

/// <summary>
/// Regras de alerta de metas a partir do progresso calculado. Puro e testável.
///
/// Uma meta gera no máximo UM alerta por estado por período (a chave de
/// referência inclui o período), evitando notificações excessivas.
/// </summary>
public static class GoalAlertEvaluator
{
    public static GoalAlertDescriptor? Evaluate(
        Guid goalId, string title, GoalKind kind, GoalProgress progress, string periodKey)
    {
        var pct = (int)Math.Round(progress.Percent);
        var t = title.Trim();

        switch (progress.State)
        {
            case CalculatedGoalState.Exceeded:
                return new GoalAlertDescriptor(NotificationKind.GoalExceeded,
                    "Limite ultrapassado",
                    $"{t} · Você já usou {pct}% do limite.",
                    $"goal-exceeded:{goalId}:{periodKey}");

            case CalculatedGoalState.Attention when kind == GoalKind.Expense:
                return new GoalAlertDescriptor(NotificationKind.GoalWarning,
                    "Atenção ao limite",
                    $"{t} · {pct}% do limite já utilizado.",
                    $"goal-warning:{goalId}:{periodKey}");

            case CalculatedGoalState.Attention:
                return new GoalAlertDescriptor(NotificationKind.GoalWarning,
                    "Ritmo abaixo do esperado",
                    $"{t} · {pct}% da meta; abaixo do ritmo necessário.",
                    $"goal-behind:{goalId}:{periodKey}");

            case CalculatedGoalState.Overdue:
                return new GoalAlertDescriptor(NotificationKind.GoalOverdue,
                    "Prazo encerrado",
                    $"{t} · Período encerrado com {pct}% da meta.",
                    $"goal-overdue:{goalId}:{periodKey}");

            case CalculatedGoalState.Achieved:
                return new GoalAlertDescriptor(NotificationKind.GoalAchieved,
                    "Meta atingida 🎉",
                    $"{t} · Você alcançou {pct}% da meta.",
                    $"goal-achieved:{goalId}:{periodKey}");

            default:
                return null;
        }
    }

    /// <summary>Alertas de acompanhamento (não conclusão) são gated por "abaixo do esperado".</summary>
    public static bool IsAttentionKind(NotificationKind kind) =>
        kind is NotificationKind.GoalWarning or NotificationKind.GoalExceeded or NotificationKind.GoalOverdue;
}
