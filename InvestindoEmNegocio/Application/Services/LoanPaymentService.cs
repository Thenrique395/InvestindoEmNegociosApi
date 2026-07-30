using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Pagamento de parcelas de empréstimo. Cria o <see cref="LoanPayment"/>, a movimentação em
/// conta (<see cref="AccountTransaction"/> Debit), atualiza a parcela e o acompanhamento do
/// contrato — tudo em UMA transação atômica (um único SaveChanges). Idempotente por
/// <c>IdempotencyKey</c>: repetir a requisição não gera pagamento/despesa/movimentação em dobro.
/// </summary>
public class LoanPaymentService(
    ILoanContractRepository contractRepository,
    ILoanInstallmentRepository installmentRepository,
    ILoanPaymentRepository paymentRepository,
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    IReceiptStorageService receiptStorageService,
    ILogger<LoanPaymentService> logger) : ILoanPaymentService
{
    public async Task<LoanPaymentResult> PayAsync(Guid userId, Guid contractId, Guid installmentId, LoanPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        // Idempotência: se já existe pagamento com esta chave, devolve o estado atual sem duplicar.
        var replay = await paymentRepository.GetByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
        if (replay is not null)
            return await BuildResultForExistingAsync(userId, replay, cancellationToken);

        var contract = await contractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        if (contract.Status is LoanStatus.Closed or LoanStatus.Cancelled or LoanStatus.Archived)
            throw new AppProblemException("Contrato não ativo", "O contrato não está ativo e não aceita novos pagamentos.", StatusCodes.Status409Conflict, code: "loan_not_active");

        var installment = await installmentRepository.GetByIdAsync(installmentId, userId, cancellationToken)
            ?? throw new AppProblemException("Parcela não encontrada", "A parcela informada não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        if (installment.ContractId != contract.Id)
            throw new AppProblemException("Parcela inválida", "A parcela não pertence ao contrato informado.", StatusCodes.Status400BadRequest);

        if (installment.Status == LoanInstallmentStatus.Paid)
            throw new AppProblemException("Parcela já paga", "A parcela já foi paga.", StatusCodes.Status409Conflict, code: "installment_already_paid");

        if (request.PenaltyAmount < 0 || request.DiscountAmount < 0)
            throw new AppProblemException("Valores inválidos", "Multa e desconto não podem ser negativos.", StatusCodes.Status400BadRequest);

        var penalty = LoanCalculator.RoundMoney(request.PenaltyAmount);
        var discount = LoanCalculator.RoundMoney(request.DiscountAmount);
        var amount = request.AmountPaid.HasValue
            ? LoanCalculator.RoundMoney(request.AmountPaid.Value)
            : LoanCalculator.RoundMoney(installment.TotalAmount + penalty - discount);

        if (amount <= 0)
            throw new AppProblemException("Valor inválido", "O valor pago deve ser maior que zero.", StatusCodes.Status400BadRequest);

        var paidAtUtc = request.PaidAt.ToUniversalTime();

        // Conta é opcional; quando informada, valida a posse antes de movimentar.
        Account? account = null;
        if (request.AccountId is Guid accountId)
        {
            account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken)
                ?? throw new AppProblemException("Conta inválida", "A conta informada não existe ou não pertence ao usuário.", StatusCodes.Status400BadRequest, code: "invalid_account");
        }

        var payment = new LoanPayment(
            contract.Id, installment.Id, userId, contract.SpaceId, paidAtUtc, amount,
            installment.PrincipalAmount, installment.InterestAmount, idempotencyKey,
            penalty, discount, account?.Id, request.MethodId, request.Note);
        await paymentRepository.AddAsync(payment, cancellationToken);

        if (account is not null)
        {
            var transaction = new AccountTransaction(
                account.Id, userId, account.SpaceId, paidAtUtc,
                AccountTransactionKind.Debit, amount,
                $"Pagamento parcela {installment.InstallmentNo} - {contract.Title}",
                AccountTransactionSourceTypes.LoanPayment, payment.Id);
            await accountTransactionRepository.AddAsync(transaction, cancellationToken);
            payment.LinkAccountTransaction(transaction.Id);
        }

        installment.RegisterFullPayment(paidAtUtc, penalty, discount);
        RecomputeTracking(contract, installment, changedIsPaid: true,
            await installmentRepository.ListByContractAsync(contract.Id, userId, cancellationToken));

        try
        {
            // Um único SaveChanges = transação atômica (pagamento + movimentação + parcela + contrato).
            await paymentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Corrida de idempotência: outra requisição idêntica venceu. Devolve o pagamento vencedor.
            var winner = await paymentRepository.GetByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            if (winner is not null && winner.Id != payment.Id)
                return await BuildResultForExistingAsync(userId, winner, cancellationToken);
            throw;
        }

        logger.LogInformation("Loan installment paid {UserId} {ContractId} {InstallmentId} {PaymentId}", userId, contractId, installmentId, payment.Id);

        var installments = await installmentRepository.ListByContractAsync(contract.Id, userId, cancellationToken);
        return BuildResult(payment, installment, contract, installments);
    }

    public async Task<LoanPaymentResult> ReverseAsync(Guid userId, Guid contractId, Guid installmentId, Guid paymentId, LoanPaymentReversalRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId, userId, cancellationToken)
            ?? throw new AppProblemException("Pagamento não encontrado", "O pagamento informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        if (payment.ContractId != contractId || payment.InstallmentId != installmentId)
            throw new AppProblemException("Pagamento inválido", "O pagamento não pertence ao contrato/parcela informados.", StatusCodes.Status400BadRequest);

        if (payment.IsReversed)
            throw new AppProblemException("Pagamento já estornado", "Este pagamento já foi estornado.", StatusCodes.Status409Conflict, code: "payment_already_reversed");

        var contract = await contractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);
        var installment = await installmentRepository.GetByIdAsync(installmentId, userId, cancellationToken)
            ?? throw new AppProblemException("Parcela não encontrada", "A parcela informada não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        // Estorno em conta (crédito de volta) quando o pagamento debitou uma conta ainda existente.
        if (payment.AccountId is Guid accountId)
        {
            var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
            if (account is not null)
            {
                var reversalTransaction = new AccountTransaction(
                    account.Id, userId, account.SpaceId, DateTime.UtcNow,
                    AccountTransactionKind.Credit, payment.Amount,
                    $"Estorno pagamento parcela {installment.InstallmentNo} - {contract.Title}",
                    AccountTransactionSourceTypes.LoanPaymentReversal, payment.Id);
                await accountTransactionRepository.AddAsync(reversalTransaction, cancellationToken);
            }
        }

        installment.ReversePayment();
        payment.MarkReversed(DateTime.UtcNow, request.Reason);
        RecomputeTracking(contract, installment, changedIsPaid: false,
            await installmentRepository.ListByContractAsync(contractId, userId, cancellationToken));

        await paymentRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Loan payment reversed {UserId} {ContractId} {PaymentId}", userId, contractId, paymentId);

        var installments = await installmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        return BuildResult(payment, installment, contract, installments);
    }

    public async Task<string?> AttachReceiptAsync(Guid userId, Guid installmentId, Guid paymentId, Stream content, string originalFileName, string contentType, string baseUrl, CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(paymentId, userId, cancellationToken);
        if (payment is null || payment.InstallmentId != installmentId) return null;

        var receiptUrl = await receiptStorageService.SaveAsync(userId, content, originalFileName, contentType, baseUrl, cancellationToken);
        payment.AttachReceipt(receiptUrl);
        await paymentRepository.SaveChangesAsync(cancellationToken);
        return receiptUrl;
    }

    public async Task<IReadOnlyList<LoanPaymentHistoryItem>> ListByInstallmentAsync(Guid userId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var payments = await paymentRepository.ListByInstallmentAsync(installmentId, userId, cancellationToken);
        return payments
            .Select(p => new LoanPaymentHistoryItem(
                p.Id, p.PaidAt, p.Amount, p.PrincipalAmount, p.InterestAmount,
                p.PenaltyAmount, p.DiscountAmount, p.AccountId, p.Note, p.ReceiptUrl,
                p.IsReversed, p.ReversedAt))
            .ToList();
    }

    /// <summary>
    /// Recalcula os totais/quitação do contrato após uma parcela mudar de estado. Robusto a
    /// rastreamento: exclui a parcela alterada da lista (estado do banco) e a soma explicitamente
    /// conforme <paramref name="changedIsPaid"/> (paga → soma em pagos; estornada → soma em abertos).
    /// </summary>
    private static void RecomputeTracking(LoanContract contract, LoanInstallment changed, bool changedIsPaid, IReadOnlyList<LoanInstallment> all)
    {
        var others = all.Where(x => x.Id != changed.Id).ToList();
        var paidOthers = others.Where(x => x.Status == LoanInstallmentStatus.Paid).ToList();
        var openOthers = others
            .Where(x => x.Status is not LoanInstallmentStatus.Paid and not LoanInstallmentStatus.Cancelled)
            .ToList();

        contract.UpdateTracking(
            openOthers.Sum(x => x.TotalAmount) + (changedIsPaid ? 0m : changed.TotalAmount),
            paidOthers.Sum(x => x.TotalAmount) + (changedIsPaid ? changed.TotalAmount : 0m),
            paidOthers.Sum(x => x.PrincipalAmount) + (changedIsPaid ? changed.PrincipalAmount : 0m),
            paidOthers.Sum(x => x.InterestAmount) + (changedIsPaid ? changed.InterestAmount : 0m));

        var noneOpen = openOthers.Count == 0 && changedIsPaid;
        if (noneOpen && contract.Status != LoanStatus.Closed)
            contract.MarkClosed();
        else if (!noneOpen && contract.Status == LoanStatus.Closed)
            contract.Reopen();
    }

    private async Task<LoanPaymentResult> BuildResultForExistingAsync(Guid userId, LoanPayment payment, CancellationToken cancellationToken)
    {
        var contract = await contractRepository.GetByIdAsync(payment.ContractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato do pagamento não foi localizado.", StatusCodes.Status404NotFound);
        var installments = await installmentRepository.ListByContractAsync(payment.ContractId, userId, cancellationToken);
        var installment = installments.FirstOrDefault(x => x.Id == payment.InstallmentId)
            ?? throw new AppProblemException("Parcela não encontrada", "A parcela do pagamento não foi localizada.", StatusCodes.Status404NotFound);
        return BuildResult(payment, installment, contract, installments);
    }

    private static LoanPaymentResult BuildResult(LoanPayment payment, LoanInstallment installment, LoanContract contract, IReadOnlyList<LoanInstallment> installments)
    {
        var open = installments
            .Where(x => x.Status is not LoanInstallmentStatus.Paid and not LoanInstallmentStatus.Cancelled)
            .OrderBy(x => x.DueDate)
            .ToList();

        var summary = new LoanContractSummary(
            contract.Id,
            contract.Status,
            contract.OpenBalance,
            contract.PaidAmount,
            contract.PaidPrincipal,
            contract.PaidInterest,
            open.Count,
            open.FirstOrDefault()?.DueDate,
            contract.MonthlyPayment);

        var installmentResponse = new LoanInstallmentResponse(
            installment.Id,
            installment.InstallmentNo,
            installment.DueDate,
            installment.BeginningBalance,
            installment.PrincipalAmount,
            installment.InterestAmount,
            installment.TotalAmount,
            installment.EndingBalance,
            installment.Status,
            installment.PaidAt);

        return new LoanPaymentResult(
            payment.Id,
            payment.ContractId,
            payment.InstallmentId,
            payment.Amount,
            payment.PrincipalAmount,
            payment.InterestAmount,
            payment.PenaltyAmount,
            payment.DiscountAmount,
            payment.PaidAt,
            payment.AccountTransactionId,
            payment.ReceiptUrl,
            installmentResponse,
            summary);
    }
}
