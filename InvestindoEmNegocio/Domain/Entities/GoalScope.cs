using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

/// <summary>
/// Vínculo de uma meta a um dado financeiro do usuário (categoria, conta ou
/// portfólio). Uma meta pode ter vários escopos (ex.: várias categorias).
/// Escopo vazio = todas as movimentações do tipo da meta.
/// </summary>
public class GoalScope
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GoalId { get; private set; }
    public GoalScopeType ScopeType { get; private set; }
    public Guid RefId { get; private set; }

    private GoalScope() { }

    public GoalScope(Guid goalId, GoalScopeType scopeType, Guid refId)
    {
        GoalId = goalId;
        ScopeType = scopeType;
        RefId = refId;
    }
}
