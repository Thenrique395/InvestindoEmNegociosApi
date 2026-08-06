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
    ICurrentSpaceAccessor currentSpaceAccessor,
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

        var installments = await installmentRepository.ListByPlanAsync(id, userId, cancellationToken) ?? [];
        var installmentIds = installments.Select(i => i.Id).ToList();
        var payments = await paymentRepository.ListByInstallmentIdsAsync(installmentIds, cancellationToken) ?? [];
        await CleanupLedgerFromPaymentsAsync(userId, payments, cancellationToken);
        paymentRepository.RemoveRange(payments);
        installmentRepository.RemoveRange(installments);

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

        await GenerateInstallmentsAsync(plan, cancellationToken);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plan updated {UserId} {PlanId} {Schedule}", userId, plan.Id, plan.Schedule);

        return CreatePlanResponse(plan);
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

    private async Task GenerateInstallmentsAsync(MoneyPlan plan, CancellationToken cancellationToken)
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
                await installmentRepository.AddAsync(BuildInstallment(plan, 1, plan.StartDate, card), cancellationToken);
                return;
            case ScheduleType.Installments when plan.InstallmentsCount.HasValue:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= plan.InstallmentsCount.Value; i++)
                {
                    var purchaseDate = plan.StartDate.AddMonths(i - 1);
                    list.Add(BuildInstallment(plan, i, purchaseDate, card));
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
                    list.Add(BuildInstallment(plan, i, purchaseDate, card));
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
        new(p.Id, p.Type, p.Title, p.Amount, p.Schedule, p.Frequency, p.InstallmentsCount, p.StartDate, p.Status.ToString(), p.CategoryId, p.CardId);
}
