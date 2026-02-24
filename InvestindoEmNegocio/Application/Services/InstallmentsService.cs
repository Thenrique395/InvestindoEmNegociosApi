using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InstallmentsService(
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPaymentRepository paymentRepository,
    IMoneyPlanRepository planRepository,
    IUserRepository userRepository,
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ILogger<InstallmentsService> logger)
    : IInstallmentsService
{
    private readonly ILogger<InstallmentsService> _logger = logger;
    public async Task<IReadOnlyList<InstallmentResponse>> ListAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default)
    {
        var data = await installmentRepository.ListByUserAsync(userId, status, from, to, type, cancellationToken);
        return data.Select(i => new InstallmentResponse(
            i.Id,
            i.PlanId,
            i.InstallmentNo,
            i.DueDate,
            i.Amount,
            i.Status,
            i.StatementYear,
            i.StatementMonth,
            i.StatementCloseDate,
            i.StatementDueDate)).ToList();
    }

    public async Task<IReadOnlyList<InstallmentPaymentResponse>?> ListPaymentsAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return null;

        var payments = await paymentRepository.ListByInstallmentIdAsync(installmentId, cancellationToken);
        if (payments.Count == 0) return [];

        var positivePaymentIds = payments
            .Where(p => p.PaidAmount > 0)
            .Select(p => p.Id)
            .ToList();

        var reversals = await accountTransactionRepository.ListBySourceAsync(
            userId,
            "InstallmentPaymentReversal",
            positivePaymentIds,
            cancellationToken) ?? [];
        var reversedIds = reversals
            .Select(r => r.SourceId)
            .Distinct()
            .ToHashSet();

        return payments
            .OrderByDescending(p => p.PaidAt)
            .Select(p =>
            {
                var isReversal = p.PaidAmount < 0;
                var canReverse = p.PaidAmount > 0 && !reversedIds.Contains(p.Id);
                return new InstallmentPaymentResponse(
                    p.Id,
                    p.PaidAt,
                    p.PaidAmount,
                    p.MethodId,
                    p.Note,
                    isReversal,
                    canReverse);
            })
            .ToList();
    }

    public async Task<bool> PayAsync(Guid userId, Guid installmentId, PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var account = await ResolveAccountForPaymentAsync(userId, request.AccountId, cancellationToken);

        var payment = new MoneyPayment(installmentId, userId, request.PaidAt.ToUniversalTime(), request.PaidAmount, request.MethodId, request.Note, account?.Id);
        await paymentRepository.AddAsync(payment, cancellationToken);

        if (account is not null)
        {
            var plan = await planRepository.GetByIdAsync(installment.PlanId, userId, cancellationToken);
            if (plan is null)
            {
                throw new AppProblemException("Plano inválido", "Plano da parcela não encontrado.", StatusCodes.Status400BadRequest);
            }

            var transactionKind = plan.Type == MoneyType.Income
                ? AccountTransactionKind.Credit
                : AccountTransactionKind.Debit;

            var transaction = new AccountTransaction(
                account.Id,
                userId,
                request.PaidAt.ToUniversalTime(),
                transactionKind,
                request.PaidAmount,
                $"Pagamento parcela {installment.InstallmentNo} - {plan.Title}",
                "InstallmentPayment",
                payment.Id);

            await accountTransactionRepository.AddAsync(transaction, cancellationToken);
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);

        await UpdateInstallmentStatusAsync(installment, cancellationToken);
        await installmentRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Installment paid {UserId} {InstallmentId}", userId, installmentId);
        return true;
    }

    public async Task<bool> ReversePaymentAsync(
        Guid userId,
        Guid installmentId,
        Guid paymentId,
        PaymentReversalRequest request,
        CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var payment = await paymentRepository.GetByIdAsync(paymentId, userId, cancellationToken);
        if (payment is null || payment.InstallmentId != installmentId) return false;

        if (payment.PaidAmount <= 0)
            throw new AppProblemException("Pagamento inválido", "Somente pagamentos positivos podem ser estornados.", StatusCodes.Status400BadRequest);

        var existingReversalTransactions = await accountTransactionRepository.ListBySourceAsync(
            userId,
            "InstallmentPaymentReversal",
            [paymentId],
            cancellationToken) ?? [];
        if (existingReversalTransactions.Count > 0)
            throw new AppProblemException("Pagamento já estornado", "Já existe estorno para esse pagamento.", StatusCodes.Status400BadRequest);

        var reversedAt = (request.ReversedAt ?? DateTime.UtcNow).ToUniversalTime();
        var reversalNote = string.IsNullOrWhiteSpace(request.Note)
            ? $"Estorno do pagamento {paymentId}"
            : request.Note.Trim();

        var reversalPayment = new MoneyPayment(
            installmentId,
            userId,
            reversedAt,
            -payment.PaidAmount,
            payment.MethodId,
            reversalNote,
            payment.AccountId);
        await paymentRepository.AddAsync(reversalPayment, cancellationToken);

        if (payment.AccountId.HasValue)
        {
            var account = await accountRepository.GetByIdAsync(payment.AccountId.Value, userId, cancellationToken);
            if (account is not null)
            {
                var plan = await planRepository.GetByIdAsync(installment.PlanId, userId, cancellationToken);
                if (plan is null)
                    throw new AppProblemException("Plano inválido", "Plano da parcela não encontrado.", StatusCodes.Status400BadRequest);

                var reversalKind = plan.Type == MoneyType.Income
                    ? AccountTransactionKind.Debit
                    : AccountTransactionKind.Credit;

                var reversalTransaction = new AccountTransaction(
                    account.Id,
                    userId,
                    reversedAt,
                    reversalKind,
                    payment.PaidAmount,
                    $"Estorno pagamento parcela {installment.InstallmentNo} - {plan.Title}",
                    "InstallmentPaymentReversal",
                    paymentId);

                await accountTransactionRepository.AddAsync(reversalTransaction, cancellationToken);
            }
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);
        await UpdateInstallmentStatusAsync(installment, cancellationToken);
        await installmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Installment payment reversed {UserId} {InstallmentId} {PaymentId}", userId, installmentId, paymentId);
        return true;
    }

    private async Task<Account?> ResolveAccountForPaymentAsync(Guid userId, Guid? requestedAccountId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Usuário não encontrado.");

        var activeAccounts = (await accountRepository.ListByUserAsync(userId, cancellationToken))
            .Where(a => a.IsActive)
            .ToList();

        if (activeAccounts.Count == 0)
            throw new AppProblemException("Conta obrigatória", "Nenhuma conta ativa encontrada para registrar a movimentação.", StatusCodes.Status400BadRequest);

        if (user.Role == UserRole.Basic)
            return SelectDefaultAccount(activeAccounts);

        if (!requestedAccountId.HasValue)
        {
            if (activeAccounts.Count == 1)
                return activeAccounts[0];

            throw new AppProblemException(
                "Conta obrigatória",
                "Selecione a conta para registrar o pagamento.",
                StatusCodes.Status400BadRequest);
        }

        var account = await accountRepository.GetByIdAsync(requestedAccountId.Value, userId, cancellationToken);
        if (account is null)
            throw new AppProblemException("Conta inválida", "Conta informada não encontrada.", StatusCodes.Status400BadRequest);

        if (!account.IsActive)
            throw new AppProblemException("Conta inativa", "Ative a conta para registrar movimentações.", StatusCodes.Status400BadRequest);

        return account;
    }

    private static Account SelectDefaultAccount(List<Account> activeAccounts)
    {
        return activeAccounts
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Id)
            .First();
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
        _logger.LogInformation("Installment anticipated {UserId} {InstallmentId} {DueDate}", userId, installmentId, request.DueDate);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null) return false;
        if (installment.UserId != userId) throw new UnauthorizedAccessException("Parcela pertence a outro usuário.");

        var payments = await paymentRepository.ListByInstallmentIdAsync(installmentId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToList();
        if (paymentIds.Count > 0)
        {
            var transactions = await accountTransactionRepository.ListBySourceAsync(
                userId,
                "InstallmentPayment",
                paymentIds,
                cancellationToken) ?? [];
            if (transactions.Count > 0)
            {
                accountTransactionRepository.RemoveRange(transactions);
            }
        }

        paymentRepository.RemoveRange(payments);
        installmentRepository.Remove(installment);
        await installmentRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Installment deleted {UserId} {InstallmentId} with {PaymentsCount} payments cleaned from ledger",
            userId,
            installmentId,
            paymentIds.Count);
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
