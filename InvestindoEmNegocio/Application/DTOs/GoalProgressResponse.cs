using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>
/// Progresso calculado da meta (nunca persistido). Realized = efetivado
/// (pago/recebido/aportado); Pending = previsto, exibido à parte.
/// </summary>
public record GoalProgressResponse(
    Guid GoalId,
    GoalKind Kind,
    GoalMode Mode,
    decimal Target,
    decimal Realized,
    decimal Pending,
    decimal Percent,
    decimal Remaining,
    decimal? Forecast,
    int? DaysRemaining,
    string State,
    DateOnly? Start,
    DateOnly? End);
