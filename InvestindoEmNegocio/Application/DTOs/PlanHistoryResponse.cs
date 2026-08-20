namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>
/// Um acontecimento do lançamento, pronto para a tela.
/// </summary>
/// <param name="Type">Tipo do evento (ex.: "Created", "AmountChanged").</param>
/// <param name="OccurredAt">Quando aconteceu.</param>
/// <param name="ActorName">Quem fez. Nulo quando foi o sistema.</param>
/// <param name="OldValue">Valor anterior, quando o evento tem antes e depois.</param>
/// <param name="NewValue">Valor novo, ou o dado do evento (categoria, valor pago).</param>
/// <param name="InstallmentId">Parcela envolvida, quando o evento é de uma delas.</param>
/// <param name="InstallmentNo">Número da parcela, para a tela não precisar cruzar.</param>
/// <param name="Derived">
/// `true` quando o evento não foi gravado e sim deduzido do estado atual — o caso
/// dos lançamentos criados antes de a trilha existir.
/// </param>
public record PlanHistoryEventResponse(
    string Type,
    DateTime OccurredAt,
    string? ActorName,
    string? OldValue,
    string? NewValue,
    Guid? InstallmentId,
    int? InstallmentNo,
    bool Derived);

public record PlanHistoryResponse(Guid PlanId, IReadOnlyList<PlanHistoryEventResponse> Events);
