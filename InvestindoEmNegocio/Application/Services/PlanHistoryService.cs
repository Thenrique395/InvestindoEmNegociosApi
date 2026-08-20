using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Histórico de um lançamento: o que foi gravado, mais o que dá para deduzir.
///
/// A dedução existe porque a trilha começou a existir depois dos lançamentos.
/// Sem ela, todo lançamento antigo abriria o histórico vazio — o que é pior do
/// que mostrar menos: parece defeito.
/// </summary>
public class PlanHistoryService(
    IPlanHistoryRepository historyRepository,
    IUserRepository userRepository,
    IMoneyPlanRepository planRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPaymentRepository paymentRepository,
    ICurrentSpaceAccessor currentSpaceAccessor)
    : IPlanHistoryService
{
    public async Task RecordAsync(
        Guid userId,
        Guid planId,
        PlanHistoryEventType type,
        DateTime occurredAt,
        Guid? actorUserId = null,
        Guid? installmentId = null,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        // Derivado por definição: nunca é gravado, para não duplicar com o que a
        // leitura já calcula a partir da data da parcela.
        if (type == PlanHistoryEventType.DueDatePassed) return;

        var entry = new PlanHistoryEntry(
            userId,
            currentSpaceAccessor.RequireSpaceId(),
            planId,
            type,
            occurredAt,
            actorUserId ?? userId,
            installmentId,
            oldValue,
            newValue);

        await historyRepository.AddAsync(entry, cancellationToken);
        await historyRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlanHistoryResponse?> GetAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(planId, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await installmentRepository.ListByPlanAsync(planId, userId, cancellationToken) ?? [];
        var installmentNumbers = installments.ToDictionary(i => i.Id, i => i.InstallmentNo);

        var gravados = await historyRepository.ListByPlanAsync(planId, userId, cancellationToken);

        var nomesPorUsuario = await ResolveActorNamesAsync(gravados, cancellationToken);

        var eventos = gravados
            .Select(e => new PlanHistoryEventResponse(
                e.Type.ToString(),
                e.OccurredAt,
                e.ActorUserId.HasValue ? nomesPorUsuario.GetValueOrDefault(e.ActorUserId.Value) : null,
                e.OldValue,
                e.NewValue,
                e.InstallmentId,
                e.InstallmentId.HasValue ? installmentNumbers.GetValueOrDefault(e.InstallmentId.Value) : null,
                Derived: false))
            .ToList();

        eventos.AddRange(await DeriveMissingEventsAsync(plan, installments, gravados, cancellationToken));

        // As parcelas vêm na mesma resposta de propósito: a gaveta precisa da série
        // inteira (a 12ª pode vencer no ano que vem, fora do mês carregado na tela),
        // e duas chamadas para abrir uma gaveta é uma a mais.
        var parcelas = installments
            .OrderBy(i => i.InstallmentNo)
            .Select(i => new PlanHistoryInstallmentResponse(
                i.Id,
                i.InstallmentNo,
                i.DueDate,
                i.Amount,
                i.Status.ToString()))
            .ToList();

        return new PlanHistoryResponse(
            planId,
            plan.Schedule.ToString(),
            parcelas,
            eventos.OrderBy(e => e.OccurredAt).ThenBy(e => e.Type).ToList());
    }

    private async Task<Dictionary<Guid, string>> ResolveActorNamesAsync(
        IReadOnlyCollection<PlanHistoryEntry> entries,
        CancellationToken cancellationToken)
    {
        var ids = entries.Where(e => e.ActorUserId.HasValue).Select(e => e.ActorUserId!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        var nomes = new Dictionary<Guid, string>();
        foreach (var id in ids)
        {
            var usuario = await userRepository.GetByIdAsync(id, cancellationToken);
            if (usuario is not null) nomes[id] = usuario.Name;
        }

        return nomes;
    }

    /// <summary>
    /// Eventos que o estado atual permite afirmar sem trilha: a criação do
    /// lançamento, cada pagamento registrado e o vencimento que passou.
    ///
    /// Só entram quando não há evento gravado equivalente — depois do primeiro
    /// deploy, os dois convivem no mesmo lançamento.
    /// </summary>
    private async Task<List<PlanHistoryEventResponse>> DeriveMissingEventsAsync(
        MoneyPlan plan,
        IReadOnlyCollection<MoneyInstallment> installments,
        IReadOnlyCollection<PlanHistoryEntry> gravados,
        CancellationToken cancellationToken)
    {
        var derivados = new List<PlanHistoryEventResponse>();

        if (!gravados.Any(e => e.Type == PlanHistoryEventType.Created))
        {
            derivados.Add(new PlanHistoryEventResponse(
                PlanHistoryEventType.Created.ToString(),
                plan.CreatedAt,
                ActorName: null,
                OldValue: null,
                NewValue: null,
                InstallmentId: null,
                InstallmentNo: null,
                Derived: true));
        }

        var pagamentosGravados = gravados
            .Where(e => e.Type == PlanHistoryEventType.PaymentRegistered && e.InstallmentId.HasValue)
            .Select(e => e.InstallmentId!.Value)
            .ToHashSet();

        var pagamentos = await paymentRepository.ListByInstallmentIdsAsync(
            installments.Select(i => i.Id).ToList(),
            cancellationToken) ?? [];

        foreach (var pagamento in pagamentos.Where(p => !pagamentosGravados.Contains(p.InstallmentId)))
        {
            var parcela = installments.FirstOrDefault(i => i.Id == pagamento.InstallmentId);
            derivados.Add(new PlanHistoryEventResponse(
                PlanHistoryEventType.PaymentRegistered.ToString(),
                pagamento.PaidAt,
                ActorName: null,
                OldValue: null,
                NewValue: pagamento.PaidAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                InstallmentId: pagamento.InstallmentId,
                InstallmentNo: parcela?.InstallmentNo,
                Derived: true));
        }

        // Vencimento ultrapassado é sempre derivado: ninguém "faz" isso, e guardar
        // exigiria alguém virando o estado de todo mundo à meia-noite.
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var parcela in installments.Where(i => i.Status == InstallmentStatus.Open && i.DueDate < hoje))
        {
            derivados.Add(new PlanHistoryEventResponse(
                PlanHistoryEventType.DueDatePassed.ToString(),
                parcela.DueDate.ToDateTime(TimeOnly.MinValue),
                ActorName: null,
                OldValue: null,
                NewValue: null,
                InstallmentId: parcela.Id,
                InstallmentNo: parcela.InstallmentNo,
                Derived: true));
        }

        return derivados;
    }
}
