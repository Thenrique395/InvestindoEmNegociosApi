using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>Vínculo da meta a um dado do usuário (categoria/conta/portfólio).</summary>
public record GoalScopeDto(GoalScopeType ScopeType, Guid RefId);
