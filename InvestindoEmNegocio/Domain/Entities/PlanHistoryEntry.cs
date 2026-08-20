using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

/// <summary>
/// Um acontecimento na vida de um lançamento — o que a tela mostra como
/// "Histórico".
///
/// Tabela própria, e não <see cref="AuditLog"/>, porque os dois têm donos
/// diferentes: auditoria é trilha de segurança, retida mesmo quando o usuário
/// apaga a conta; isto aqui é dado financeiro do próprio usuário, que sai junto
/// com ele. Misturar os dois também inflaria a contagem do centro de privacidade.
/// </summary>
public class PlanHistoryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public Guid PlanId { get; private set; }

    /// <summary>Preenchido quando o evento é de uma parcela específica.</summary>
    public Guid? InstallmentId { get; private set; }

    public PlanHistoryEventType Type { get; private set; }

    /// <summary>Quando aconteceu — não é o mesmo que quando foi gravado.</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>Nulo quando quem fez foi o sistema, não uma pessoa.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Valor anterior, já formatado pelo domínio (ex.: "860.00", "Saúde").</summary>
    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public PlanHistoryEntry(
        Guid userId,
        Guid spaceId,
        Guid planId,
        PlanHistoryEventType type,
        DateTime occurredAt,
        Guid? actorUserId = null,
        Guid? installmentId = null,
        string? oldValue = null,
        string? newValue = null)
    {
        UserId = userId;
        SpaceId = spaceId;
        PlanId = planId;
        Type = type;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        InstallmentId = installmentId;
        OldValue = oldValue;
        NewValue = newValue;
    }

    private PlanHistoryEntry()
    {
    }
}
