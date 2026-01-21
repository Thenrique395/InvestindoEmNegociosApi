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
    ILogger<PlansService> logger)
    : IPlansService
{
    private readonly ILogger<PlansService> _logger = logger;
    public async Task<PlanResponse> CreateAsync(Guid userId, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request);

        var plan = new MoneyPlan(
            userId,
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

        return ToResponse(plan);
    }

    public async Task<IReadOnlyList<PlanResponse>> ListAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var data = await planRepository.ListByUserAsync(userId, type, cancellationToken);
        return data.Select(ToResponse).ToList();
    }

    public async Task<PlanDetailsResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await installmentRepository.ListByPlanAsync(id, userId, cancellationToken);
        var responseInstallments = installments.Select(i => new InstallmentResponse(i.Id, i.PlanId, i.InstallmentNo, i.DueDate, i.Amount, i.Status)).ToList();
        return new PlanDetailsResponse(ToResponse(plan), responseInstallments);
    }

    public async Task<PlanResponse?> UpdateAsync(Guid userId, Guid id, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request);

        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await installmentRepository.ListByPlanAsync(id, userId, cancellationToken);
        var installmentIds = installments.Select(i => i.Id).ToList();
        var payments = await paymentRepository.ListByInstallmentIdsAsync(installmentIds, cancellationToken);
        paymentRepository.RemoveRange(payments);
        installmentRepository.RemoveRange(installments);

        plan.GetType().GetProperty("Type")?.SetValue(plan, request.Type);
        plan.GetType().GetProperty("Title")?.SetValue(plan, request.Title);
        plan.GetType().GetProperty("Amount")?.SetValue(plan, request.Amount);
        plan.GetType().GetProperty("Schedule")?.SetValue(plan, request.Schedule);
        plan.GetType().GetProperty("StartDate")?.SetValue(plan, request.StartDate);
        plan.GetType().GetProperty("Frequency")?.SetValue(plan, request.Frequency);
        plan.GetType().GetProperty("InstallmentsCount")?.SetValue(plan, request.InstallmentsCount);
        plan.GetType().GetProperty("UpdatedAt")?.SetValue(plan, DateTime.UtcNow);
        plan.GetType().GetProperty("CategoryId")?.SetValue(plan, request.CategoryId);
        plan.GetType().GetProperty("CardId")?.SetValue(plan, request.CardId);
        plan.GetType().GetProperty("DefaultPaymentMethodId")?.SetValue(plan, request.DefaultPaymentMethodId);

        await GenerateInstallmentsAsync(plan, cancellationToken);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plan updated {UserId} {PlanId} {Schedule}", userId, plan.Id, plan.Schedule);

        return ToResponse(plan);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return false;

        planRepository.Remove(plan);
        await planRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plan deleted {UserId} {PlanId}", userId, plan.Id);
        return true;
    }

    private async Task GenerateInstallmentsAsync(MoneyPlan plan, CancellationToken cancellationToken)
    {
        switch (plan.Schedule)
        {
            case ScheduleType.OneTime:
                await installmentRepository.AddAsync(new MoneyInstallment(plan.Id, plan.UserId, 1, plan.StartDate, plan.Amount), cancellationToken);
                return;
            case ScheduleType.Installments when plan.InstallmentsCount.HasValue:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= plan.InstallmentsCount.Value; i++)
                {
                    var due = plan.StartDate.AddMonths(i - 1);
                    list.Add(new MoneyInstallment(plan.Id, plan.UserId, i, due, plan.Amount));
                }
                await installmentRepository.AddRangeAsync(list, cancellationToken);
                return;
            }
            case ScheduleType.Recurring:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= 6; i++)
                {
                    var due = plan.StartDate.AddMonths(i - 1);
                    list.Add(new MoneyInstallment(plan.Id, plan.UserId, i, due, plan.Amount));
                }
                await installmentRepository.AddRangeAsync(list, cancellationToken);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ValidateSchedule(CreatePlanRequest request)
    {
        throw request.Schedule switch
        {
            ScheduleType.OneTime when request.InstallmentsCount != 1 => new ArgumentException(
                "ONE_TIME requer installmentsCount = 1."),
            ScheduleType.Installments when (request.InstallmentsCount is null || request.InstallmentsCount < 2) =>
                new ArgumentException("INSTALLMENTS requer installmentsCount >= 2."),
            ScheduleType.Recurring when request.Frequency is null => new ArgumentException(
                "RECURRING requer frequency."),
            _ => new ArgumentOutOfRangeException()
        };
    }

    private static PlanResponse ToResponse(MoneyPlan p) =>
        new(p.Id, p.Type, p.Title, p.Amount, p.Schedule, p.Frequency, p.InstallmentsCount, p.StartDate, p.Status.ToString(), p.CategoryId, p.CardId);
}
