using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class InstallmentsService(
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPaymentRepository paymentRepository)
    : IInstallmentsService
{
    public async Task<IReadOnlyList<InstallmentResponse>> ListAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var data = await installmentRepository.ListByUserAsync(userId, status, from, to, type, cancellationToken);
        return data.Select(i => new InstallmentResponse(i.Id, i.PlanId, i.InstallmentNo, i.DueDate, i.Amount, i.Status)).ToList();
    }

    public async Task<bool> PayAsync(Guid userId, Guid installmentId, PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var payment = new MoneyPayment(installmentId, userId, request.PaidAt.ToUniversalTime(), request.PaidAmount, request.MethodId, request.Note);
        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        await UpdateInstallmentStatusAsync(installment, cancellationToken);
        await installmentRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AnticipateAsync(Guid userId, Guid installmentId, AnticipationRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (installment.DueDate.Year == today.Year && installment.DueDate.Month == today.Month)
            throw new InvalidOperationException("Não é possível antecipar parcelas do mês atual.");

        if (installment.OriginalDueDate is null)
            installment.GetType().GetProperty("OriginalDueDate")?.SetValue(installment, installment.DueDate);

        installment.GetType().GetProperty("DueDate")?.SetValue(installment, request.DueDate);
        installment.GetType().GetProperty("Status")?.SetValue(installment, InstallmentStatus.Anticipated);
        installment.GetType().GetProperty("UpdatedAt")?.SetValue(installment, DateTime.UtcNow);

        await installmentRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null) return false;
        if (installment.UserId != userId) throw new UnauthorizedAccessException("Parcela pertence a outro usuário.");

        var payments = await paymentRepository.ListByInstallmentIdAsync(installmentId, cancellationToken);
        paymentRepository.RemoveRange(payments);
        installmentRepository.Remove(installment);
        await installmentRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task UpdateInstallmentStatusAsync(MoneyInstallment installment, CancellationToken cancellationToken)
    {
        var totalPaid = await paymentRepository.SumPaidAmountAsync(installment.Id, cancellationToken);

        if (totalPaid <= 0)
            installment.GetType().GetProperty("Status")?.SetValue(installment, InstallmentStatus.Open);
        else if (totalPaid < installment.Amount)
            installment.GetType().GetProperty("Status")?.SetValue(installment, InstallmentStatus.PartiallyPaid);
        else
            installment.GetType().GetProperty("Status")?.SetValue(installment, InstallmentStatus.Paid);
    }
}
