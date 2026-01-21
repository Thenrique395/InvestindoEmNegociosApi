using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class PlansService : IPlansService
{
    private readonly IMoneyPlanRepository _planRepository;
    private readonly IMoneyInstallmentRepository _installmentRepository;
    private readonly IMoneyPaymentRepository _paymentRepository;

    public PlansService(IMoneyPlanRepository planRepository, IMoneyInstallmentRepository installmentRepository, IMoneyPaymentRepository paymentRepository)
    {
        _planRepository = planRepository;
        _installmentRepository = installmentRepository;
        _paymentRepository = paymentRepository;
    }

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

        await _planRepository.AddAsync(plan, cancellationToken);
        await GenerateInstallmentsAsync(plan, cancellationToken);
        await _planRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(plan);
    }

    public async Task<IReadOnlyList<PlanResponse>> ListAsync(Guid userId, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var data = await _planRepository.ListByUserAsync(userId, type, cancellationToken);
        return data.Select(ToResponse).ToList();
    }

    public async Task<PlanDetailsResponse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await _installmentRepository.ListByPlanAsync(id, userId, cancellationToken);
        var responseInstallments = installments.Select(i => new InstallmentResponse(i.Id, i.PlanId, i.InstallmentNo, i.DueDate, i.Amount, i.Status)).ToList();
        return new PlanDetailsResponse(ToResponse(plan), responseInstallments);
    }

    public async Task<PlanResponse?> UpdateAsync(Guid userId, Guid id, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request);

        var plan = await _planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return null;

        var installments = await _installmentRepository.ListByPlanAsync(id, userId, cancellationToken);
        var installmentIds = installments.Select(i => i.Id).ToList();
        var payments = await _paymentRepository.ListByInstallmentIdsAsync(installmentIds, cancellationToken);
        _paymentRepository.RemoveRange(payments);
        _installmentRepository.RemoveRange(installments);

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
        await _planRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(plan);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, userId, cancellationToken);
        if (plan is null) return false;

        _planRepository.Remove(plan);
        await _planRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task GenerateInstallmentsAsync(MoneyPlan plan, CancellationToken cancellationToken)
    {
        switch (plan.Schedule)
        {
            case ScheduleType.OneTime:
                await _installmentRepository.AddAsync(new MoneyInstallment(plan.Id, plan.UserId, 1, plan.StartDate, plan.Amount), cancellationToken);
                return;
            case ScheduleType.Installments when plan.InstallmentsCount.HasValue:
            {
                var list = new List<MoneyInstallment>();
                for (var i = 1; i <= plan.InstallmentsCount.Value; i++)
                {
                    var due = plan.StartDate.AddMonths(i - 1);
                    list.Add(new MoneyInstallment(plan.Id, plan.UserId, i, due, plan.Amount));
                }
                await _installmentRepository.AddRangeAsync(list, cancellationToken);
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
                await _installmentRepository.AddRangeAsync(list, cancellationToken);
                break;
            }
        }
    }

    private static void ValidateSchedule(CreatePlanRequest request)
    {
        if (request.Schedule == ScheduleType.OneTime && request.InstallmentsCount != 1)
            throw new ArgumentException("ONE_TIME requer installmentsCount = 1.");

        if (request.Schedule == ScheduleType.Installments && (request.InstallmentsCount is null || request.InstallmentsCount < 2))
            throw new ArgumentException("INSTALLMENTS requer installmentsCount >= 2.");

        if (request.Schedule == ScheduleType.Recurring && request.Frequency is null)
            throw new ArgumentException("RECURRING requer frequency.");
    }

    private static PlanResponse ToResponse(MoneyPlan p) =>
        new(p.Id, p.Type, p.Title, p.Amount, p.Schedule, p.Frequency, p.InstallmentsCount, p.StartDate, p.Status.ToString(), p.CategoryId, p.CardId);
}
