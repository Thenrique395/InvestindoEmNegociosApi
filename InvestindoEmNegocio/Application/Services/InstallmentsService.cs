using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Common;
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
    IReceiptStorageService receiptStorageService,
    IPlanHistoryService planHistoryService,
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
            i.StatementDueDate,
            FormatStatementReference(i.StatementYear, i.StatementMonth))).ToList();
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
            AccountTransactionSourceTypes.InstallmentPaymentReversal,
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
                    canReverse,
                    p.ReceiptUrl);
            })
            .ToList();
    }

    public async Task<bool> PayAsync(Guid userId, Guid installmentId, PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var account = await ResolveAccountForPaymentAsync(userId, request.AccountId, cancellationToken);

        var payment = new MoneyPayment(installmentId, userId, installment.SpaceId, request.PaidAt.ToUniversalTime(), request.PaidAmount, request.MethodId, request.Note, account?.Id);
        await paymentRepository.AddAsync(payment, cancellationToken);

        if (account is not null)
        {
            var plan = await planRepository.GetByIdAsync(installment.PlanId, userId, cancellationToken);
            if (plan is null)
            {
                throw new AppProblemException("Plano inválido", "Plano parcelado não encontrado.", StatusCodes.Status400BadRequest);
            }

            var transactionKind = plan.Type == MoneyType.Income
                ? AccountTransactionKind.Credit
                : AccountTransactionKind.Debit;

            var transaction = new AccountTransaction(
                account.Id,
                userId,
                account.SpaceId,
                request.PaidAt.ToUniversalTime(),
                transactionKind,
                request.PaidAmount,
                $"Pagamento parcela {installment.InstallmentNo} - {plan.Title}",
                AccountTransactionSourceTypes.InstallmentPayment,
                payment.Id);

            await accountTransactionRepository.AddAsync(transaction, cancellationToken);
        }

        // Atualiza o status ANTES de salvar, computando o total como (pagamentos já
        // existentes) + este pagamento. Resultado idêntico ao antigo (que somava após
        // o save, incluindo o novo), mas agora em UMA transação atômica: pagamento,
        // transação e status da parcela persistem juntos (era 2 SaveChanges).
        var existingPaid = await paymentRepository.SumPaidAmountAsync(installment.Id, cancellationToken);
        installment.RefreshPaymentStatus(existingPaid + request.PaidAmount);

        await paymentRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Installment paid {UserId} {InstallmentId}", userId, installmentId);

        await planHistoryService.RecordAsync(
            userId,
            installment.PlanId,
            PlanHistoryEventType.PaymentRegistered,
            request.PaidAt.ToUniversalTime(),
            actorUserId: userId,
            installmentId: installmentId,
            newValue: request.PaidAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken: cancellationToken);

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
            AccountTransactionSourceTypes.InstallmentPaymentReversal,
            [paymentId],
            cancellationToken) ?? [];
        if (existingReversalTransactions.Count > 0)
            throw new AppProblemException("Pagamento já estornado", "Já existe um estorno para este pagamento.", StatusCodes.Status400BadRequest);

        var reversedAt = (request.ReversedAt ?? DateTime.UtcNow).ToUniversalTime();
        var reversalNote = string.IsNullOrWhiteSpace(request.Note)
            ? $"Estorno do pagamento {paymentId}"
            : request.Note.Trim();

        var reversalPayment = new MoneyPayment(
            installmentId,
            userId,
            installment.SpaceId,
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
                    throw new AppProblemException("Plano inválido", "Plano parcelado não encontrado.", StatusCodes.Status400BadRequest);

                var reversalKind = plan.Type == MoneyType.Income
                    ? AccountTransactionKind.Debit
                    : AccountTransactionKind.Credit;

                var reversalTransaction = new AccountTransaction(
                    account.Id,
                    userId,
                    account.SpaceId,
                    reversedAt,
                    reversalKind,
                    payment.PaidAmount,
                    $"Estorno pagamento parcela {installment.InstallmentNo} - {plan.Title}",
                    AccountTransactionSourceTypes.InstallmentPaymentReversal,
                    paymentId);

                await accountTransactionRepository.AddAsync(reversalTransaction, cancellationToken);
            }
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);
        await UpdateInstallmentStatusAsync(installment, cancellationToken);
        await installmentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Installment payment reversed {UserId} {InstallmentId} {PaymentId}", userId, installmentId, paymentId);

        await planHistoryService.RecordAsync(
            userId,
            installment.PlanId,
            PlanHistoryEventType.PaymentReversed,
            DateTime.UtcNow,
            actorUserId: userId,
            installmentId: installmentId,
            cancellationToken: cancellationToken);

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
            throw new AppProblemException("Conta obrigatória", "Nenhuma conta ativa foi encontrada para registrar a transação.", StatusCodes.Status400BadRequest);

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
            throw new AppProblemException("Conta inválida", "A conta informada não foi encontrada.", StatusCodes.Status400BadRequest);

        if (!account.IsActive)
            throw new AppProblemException("Conta inativa", "Ative a conta para registrar transações.", StatusCodes.Status400BadRequest);

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

    private static string? FormatStatementReference(int? year, int? month)
    {
        if (!year.HasValue || !month.HasValue)
            return null;
        return $"{month.Value:D2}/{year.Value}";
    }

    public async Task<string?> AttachReceiptAsync(
        Guid userId,
        Guid installmentId,
        Guid paymentId,
        Stream content,
        string originalFileName,
        string contentType,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId, userId, cancellationToken);
        if (payment is null || payment.InstallmentId != installmentId) return null;

        var receiptUrl = await receiptStorageService.SaveAsync(userId, content, originalFileName, contentType, baseUrl, cancellationToken);
        payment.AttachReceipt(receiptUrl);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        return receiptUrl;
    }

    public async Task<bool> AnticipateAsync(Guid userId, Guid installmentId, AnticipationRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        var today = BrazilTime.TodayLocal;
        installment.Anticipate(request.DueDate, today);

        await installmentRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Installment anticipated {UserId} {InstallmentId} {DueDate}", userId, installmentId, request.DueDate);
        return true;
    }

    public async Task<bool> UpdateAsync(Guid userId, Guid installmentId, UpdateInstallmentRequest request, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        if (installment is null || installment.UserId != userId) return false;

        if (installment.Status is InstallmentStatus.Canceled or InstallmentStatus.Anticipated)
            throw new InvalidOperationException("Não é possível editar uma parcela cancelada ou antecipada.");

        // Capturados antes do Edit, senão o "de" já virou o "para".
        var valorAnterior = installment.Amount;
        var vencimentoAnterior = installment.DueDate;

        installment.Edit(request.Amount, request.DueDate);

        // Preserva os pagamentos e rederiva o status a partir do total líquido pago
        // (estornos são registrados como pagamentos negativos).
        var payments = await paymentRepository.ListByInstallmentIdAsync(installmentId, cancellationToken);
        installment.RefreshPaymentStatus(payments.Sum(p => p.PaidAmount));

        await installmentRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Installment updated {UserId} {InstallmentId} {Amount} {DueDate}", userId, installmentId, request.Amount, request.DueDate);

        var agora = DateTime.UtcNow;
        var cultura = System.Globalization.CultureInfo.InvariantCulture;

        if (valorAnterior != installment.Amount)
        {
            await planHistoryService.RecordAsync(
                userId, installment.PlanId, PlanHistoryEventType.AmountChanged, agora,
                actorUserId: userId,
                installmentId: installmentId,
                oldValue: valorAnterior.ToString("0.00", cultura),
                newValue: installment.Amount.ToString("0.00", cultura),
                cancellationToken: cancellationToken);
        }

        if (vencimentoAnterior != installment.DueDate)
        {
            await planHistoryService.RecordAsync(
                userId, installment.PlanId, PlanHistoryEventType.DueDateChanged, agora,
                actorUserId: userId,
                installmentId: installmentId,
                oldValue: vencimentoAnterior.ToString("yyyy-MM-dd"),
                newValue: installment.DueDate.ToString("yyyy-MM-dd"),
                cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var installment = await installmentRepository.GetByIdAsync(installmentId, cancellationToken);
        // Dono errado é tratado como "não encontrado" (return false → 404), igual aos
        // demais métodos deste serviço — evita vazar a existência da parcela de outro usuário.
        if (installment is null || installment.UserId != userId) return false;

        var now = DateTime.UtcNow;
        var payments = await paymentRepository.ListByInstallmentIdAsync(installmentId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToList();
        if (paymentIds.Count > 0)
        {
            var transactions = await accountTransactionRepository.ListBySourceAsync(
                userId,
                "InstallmentPayment",
                paymentIds,
                cancellationToken) ?? [];
            foreach (var transaction in transactions)
                transaction.MarkDeleted(now);
        }

        foreach (var payment in payments)
            payment.MarkDeleted(now);

        installment.MarkDeleted(now);
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
        installment.RefreshPaymentStatus(totalPaid);
    }
}
