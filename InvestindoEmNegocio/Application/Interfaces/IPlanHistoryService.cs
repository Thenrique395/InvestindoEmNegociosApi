using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IPlanHistoryService
{
    /// <summary>
    /// Grava um acontecimento do lançamento. Chamado pelos serviços de domínio,
    /// que são quem conhece o antes e o depois — o controller só vê o depois.
    /// </summary>
    Task RecordAsync(
        Guid userId,
        Guid planId,
        PlanHistoryEventType type,
        DateTime occurredAt,
        Guid? actorUserId = null,
        Guid? installmentId = null,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Histórico do lançamento, do mais antigo para o mais novo, com os eventos
    /// deduzidos do estado atual para o que aconteceu antes da trilha existir.
    /// Devolve `null` quando o plano não é do usuário.
    /// </summary>
    Task<PlanHistoryResponse?> GetAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default);
}
