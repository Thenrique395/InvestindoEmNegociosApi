using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class PlansService(
    IMoneyPlanRepository planRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPaymentRepository paymentRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ICardRepository cardRepository,
    ICategoryRepository categoryRepository,
    ICurrentSpaceAccessor currentSpaceAccessor,
    IPlanHistoryService planHistoryService,
    ILogger<PlansService> logger)
    : IPlansService
{
    private readonly ILogger<PlansService> _logger = logger;

    /// <summary>
    /// Quantos meses de parcelas um plano recorrente materializa a partir da data
    /// inicial. Antes eram apenas 6 meses (a recorrência "sumia" no 7º mês). 60 meses
    /// (5 anos) cobre qualquer uso realista sem depender de job/cron. Para recorrência
    /// verdadeiramente infinita, evoluir para um job mensal de top-up ou projeção.
    /// </summary>
    public const int RecurringHorizonMonths = 60;

    public async Task<PlanResponse> CreateAsync(Guid userId, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = new MoneyPlan(
            userId,
            currentSpaceAccessor.RequireSpaceId(),
            request.Type,
            request.Title,
            request.Amount,
            request.Schedule,
            request.StartDate,
            request.Frequency,
            request.InstallmentsCount,
            request.DefaultPaymentMethodId,
            request.CategoryId,
            request.CardId);

        await planRepository.AddAsync(plan, cancellationToken);
        await GenerateInstallmentsAsync(plan, cancellationToken);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plan created {UserId} {PlanId} {Schedule}", userId, plan.Id, plan.Schedule);

        await planHistoryService.RecordAsync(
            userId,
            plan.Id,
            PlanHistoryEventType.Created,
            plan.CreatedAt,
            actorUserId: userId,
            cancellationToken: cancellationToken);

        return CreatePlanResponse(plan);
    }

    public async Task<IReadOnlyList<PlanResponse>> ListAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var data = await planRepository.ListByUserAsync(userId, type, cancellationToken);
        return data.Select(CreatePlanResponse).ToList();
    }

    public async Task<PlanDetailsResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await installmentRepository.ListByPlanAsync(id, userId, cancellationToken);
        var responseInstallments = installments.Select(i => new InstallmentResponse(
            i.Id,
            i.PlanId,
            i.InstallmentNo,
            i.DueDate,
            i.Amount,
            i.Status,
            i.StatementYear,
            i.StatementMonth,
            i.StatementCloseDate,
            i.StatementDueDate,
            FormatStatementReference(i.StatementYear, i.StatementMonth))).ToList();
        return new PlanDetailsResponse(CreatePlanResponse(plan), responseInstallments);
    }

    public async Task<PlanResponse?> UpdateAsync(Guid userId, Guid id, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        /*
         * Editar a recorrência inteira PRESERVA o que já foi pago.
         *
         * Antes, esta operação apagava TODAS as parcelas e TODOS os pagamentos do plano e
         * regerava do zero. Mudar a data de vencimento das parcelas futuras destruía o
         * histórico financeiro do passado junto — inclusive a transação na conta.
         *
         * E ela nem chegava a funcionar: o delete das antigas e o insert das novas iam na
         * MESMA `SaveChangesAsync`, o EF não garante que o DELETE preceda o INSERT na mesma
         * tabela, e o índice único `(PlanId, InstallmentNo)` era violado — 500 para quem
         * tentasse. Por isso o salvamento agora acontece em DUAS fases.
         */
        var installments = await installmentRepository.ListByPlanAsync(id, userId, cancellationToken) ?? [];

        var preservadas = installments
            .Where(i => i.Status is InstallmentStatus.Paid
                     or InstallmentStatus.PartiallyPaid
                     or InstallmentStatus.Anticipated)
            .ToList();
        var descartaveis = installments.Except(preservadas).ToList();

        var idsDescartaveis = descartaveis.Select(i => i.Id).ToList();
        var pagamentosDescartaveis = idsDescartaveis.Count > 0
            ? await paymentRepository.ListByInstallmentIdsAsync(idsDescartaveis, cancellationToken) ?? []
            : [];

        await CleanupLedgerFromPaymentsAsync(userId, pagamentosDescartaveis, cancellationToken);
        paymentRepository.RemoveRange(pagamentosDescartaveis);
        installmentRepository.RemoveRange(descartaveis);

        // FASE 1: remove antes de inserir. Sem isto, o insert pode chegar primeiro no banco
        // e colidir com a parcela antiga de mesmo `InstallmentNo`.
        await installmentRepository.SaveChangesAsync(cancellationToken);

        // Capturado antes do Update: depois disso o "de" já virou o "para".
        var valorAnterior = plan.Amount;
        var categoriaAnterior = plan.CategoryId;
        var tituloAnterior = plan.Title;

        plan.Update(
            request.Type,
            request.Title,
            request.Amount,
            request.Schedule,
            request.StartDate,
            request.Frequency,
            request.InstallmentsCount,
            request.DefaultPaymentMethodId,
            request.CategoryId,
            request.CardId);

        // FASE 2: as novas continuam a numeração das preservadas, para não reusar um
        // `InstallmentNo` que ainda existe.
        var proximoNumero = preservadas.Count > 0 ? preservadas.Max(i => i.InstallmentNo) + 1 : 1;
        await GenerateInstallmentsAsync(plan, cancellationToken, proximoNumero);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Plan updated {UserId} {PlanId} {Schedule} preservadas={Preservadas} regeradas={Regeradas}",
            userId, plan.Id, plan.Schedule, preservadas.Count, descartaveis.Count);

        await RecordChangesAsync(userId, plan, valorAnterior, categoriaAnterior, tituloAnterior, cancellationToken);

        return CreatePlanResponse(plan);
    }

    /// <summary>
    /// Um evento por campo que mudou de verdade. Salvar "editado" sem dizer o quê
    /// deixaria o histórico com a informação que menos importa.
    /// </summary>
    private async Task RecordChangesAsync(
        Guid userId,
        MoneyPlan plan,
        decimal valorAnterior,
        Guid? categoriaAnterior,
        string tituloAnterior,
        CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        if (valorAnterior != plan.Amount)
        {
            await planHistoryService.RecordAsync(
                userId, plan.Id, PlanHistoryEventType.AmountChanged, agora,
                actorUserId: userId,
                oldValue: FormatAmount(valorAnterior),
                newValue: FormatAmount(plan.Amount),
                cancellationToken: cancellationToken);
        }

        if (categoriaAnterior != plan.CategoryId)
        {
            var nomeAnterior = await ResolveCategoryNameAsync(categoriaAnterior, userId, cancellationToken);
            var nomeNovo = await ResolveCategoryNameAsync(plan.CategoryId, userId, cancellationToken);
            await planHistoryService.RecordAsync(
                userId, plan.Id, PlanHistoryEventType.CategoryChanged, agora,
                actorUserId: userId,
                oldValue: nomeAnterior,
                newValue: nomeNovo,
                cancellationToken: cancellationToken);
        }

        if (!string.Equals(tituloAnterior, plan.Title, StringComparison.Ordinal))
        {
            await planHistoryService.RecordAsync(
                userId, plan.Id, PlanHistoryEventType.TitleChanged, agora,
                actorUserId: userId,
                oldValue: tituloAnterior,
                newValue: plan.Title,
                cancellationToken: cancellationToken);
        }
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Nome da categoria para o histórico. Categoria padrão do sistema e categoria
    /// do usuário vêm por caminhos diferentes; o histórico só quer o nome.
    /// </summary>
    private async Task<string?> ResolveCategoryNameAsync(Guid? categoryId, Guid userId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue) return null;

        var doUsuario = await categoryRepository.GetByIdForUserAsync(categoryId.Value, userId, cancellationToken);
        if (doUsuario is not null) return doUsuario.Name;

        var padrao = await categoryRepository.GetDefaultByIdAsync(categoryId.Value, cancellationToken);
        return padrao?.Name;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return false;

        var now = DateTime.UtcNow;
        var installments = await installmentRepository.ListByPlanAsync(plan.Id, userId, cancellationToken, track: true) ?? [];
        var installmentIds = installments.Select(i => i.Id).ToList();
        if (installmentIds.Count > 0)
        {
            var payments = await paymentRepository.ListByInstallmentIdsAsync(installmentIds, cancellationToken) ?? [];
            var paymentIds = payments.Select(p => p.Id).ToList();
            if (paymentIds.Count > 0)
            {
                var transactions = await accountTransactionRepository.ListBySourceAsync(
                    userId,
                    AccountTransactionSourceTypes.InstallmentPayment,
                    paymentIds,
                    cancellationToken) ?? [];
                foreach (var transaction in transactions)
                    transaction.MarkDeleted(now);
            }

            foreach (var payment in payments)
                payment.MarkDeleted(now);
        }

        foreach (var installment in installments)
            installment.MarkDeleted(now);

        plan.MarkDeleted(now);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plan deleted {UserId} {PlanId}", userId, plan.Id);
        return true;
    }

    private async Task CleanupLedgerFromPaymentsAsync(Guid userId, IReadOnlyCollection<MoneyPayment> payments, CancellationToken cancellationToken)
    {
        if (payments.Count == 0) return;

        var paymentIds = payments.Select(p => p.Id).ToList();
        var transactions = await accountTransactionRepository.ListBySourceAsync(
            userId,
            AccountTransactionSourceTypes.InstallmentPayment,
            paymentIds,
            cancellationToken) ?? [];

        if (transactions.Count == 0) return;
        accountTransactionRepository.RemoveRange(transactions);
    }

    /// <param name="numeroInicial">
    /// Primeiro `InstallmentNo` a usar. Quando a edição preserva parcelas pagas, a numeração
    /// continua depois delas — reusar um número existente viola o índice único do plano.
    /// </param>
    private async Task GenerateInstallmentsAsync(MoneyPlan plan, CancellationToken cancellationToken, int numeroInicial = 1)
    {
        var card = plan.CardId.HasValue
            ? await cardRepository.GetByIdAsync(plan.CardId.Value, plan.UserId, cancellationToken)
            : null;
        if (plan.CardId.HasValue && card is null)
        {
            throw new ArgumentException("Cartão informado não encontrado para o usuário.");
        }

        switch (plan.Schedule)
        {
            case ScheduleType.OneTime:
                await installmentRepository.AddAsync(BuildInstallment(plan, numeroInicial, plan.StartDate, card), cancellationToken);
                return;
            case ScheduleType.Installments when plan.InstallmentsCount.HasValue:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= plan.InstallmentsCount.Value; i++)
                {
                    var purchaseDate = plan.StartDate.AddMonths(i - 1);
                    list.Add(BuildInstallment(plan, numeroInicial + i - 1, purchaseDate, card));
                }
                await installmentRepository.AddRangeAsync(list, cancellationToken);
                return;
            }
            case ScheduleType.Recurring:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= RecurringHorizonMonths; i++)
                {
                    var purchaseDate = plan.StartDate.AddMonths(i - 1);
                    list.Add(BuildInstallment(plan, numeroInicial + i - 1, purchaseDate, card));
                }
                await installmentRepository.AddRangeAsync(list, cancellationToken);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static MoneyInstallment BuildInstallment(MoneyPlan plan, int installmentNo, DateOnly purchaseDate, Card? card)
    {
        if (card is null)
        {
            return new MoneyInstallment(plan.Id, plan.UserId, plan.SpaceId, installmentNo, purchaseDate, plan.Amount);
        }

        var cycle = CardStatementCycleCalculator.Calculate(
            purchaseDate,
            card.StatementCloseDay,
            card.DueDay);

        return new MoneyInstallment(
            plan.Id,
            plan.UserId,
            plan.SpaceId,
            installmentNo,
            cycle.StatementDueDate,
            plan.Amount,
            statementYear: cycle.StatementYear,
            statementMonth: cycle.StatementMonth,
            statementCloseDate: cycle.StatementCloseDate,
            statementDueDate: cycle.StatementDueDate);
    }

    private static string? FormatStatementReference(int? year, int? month)
    {
        if (!year.HasValue || !month.HasValue)
            return null;
        return $"{month.Value:D2}/{year.Value}";
    }

    private static PlanResponse CreatePlanResponse(MoneyPlan p) =>
        new(p.Id, p.Type, p.Title, p.Amount, p.Schedule, p.Frequency, p.InstallmentsCount, p.StartDate, p.Status.ToString(), p.CategoryId, p.CardId, p.DefaultPaymentMethodId);
}
