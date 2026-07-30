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
/// Amortização extraordinária de empréstimos. Simula (estimativa) e confirma (transacional):
/// grava o registro, movimenta a conta, regenera as parcelas FUTURAS (parcelas pagas são
/// preservadas) e atualiza o contrato — tudo em UMA transação. Idempotente por IdempotencyKey.
/// </summary>
public class LoanAmortizationService(
    ILoanContractRepository contractRepository,
    ILoanInstallmentRepository installmentRepository,
    ILoanAmortizationRepository amortizationRepository,
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ILogger<LoanAmortizationService> logger) : ILoanAmortizationService
{
    private const string Disclaimer = "Este é um cálculo estimado. O cronograma oficial deve ser confirmado com a instituição financeira.";

    public async Task<LoanAmortizationSimulationResult> SimulateAsync(Guid userId, Guid contractId, LoanAmortizationRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(userId, contractId, request.Amount, cancellationToken);
        var outcome = ctx.Simulate(request.Strategy);
        return MapSimulation(outcome);
    }

    public async Task<LoanAmortizationResult> ConfirmAsync(Guid userId, Guid contractId, LoanAmortizationRequest request, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        var replay = await amortizationRepository.GetByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
        if (replay is not null)
            return await BuildResultForExistingAsync(userId, replay, cancellationToken);

        var ctx = await LoadContextAsync(userId, contractId, request.Amount, cancellationToken);
        var outcome = ctx.Simulate(request.Strategy);

        Account? account = null;
        if (request.AccountId is Guid accountId)
        {
            account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken)
                ?? throw new AppProblemException("Conta inválida", "A conta informada não existe ou não pertence ao usuário.", StatusCodes.Status400BadRequest, code: "invalid_account");
        }

        var newVersion = await amortizationRepository.MaxScheduleVersionAsync(contractId, userId, cancellationToken) + 1;
        var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var amortization = new LoanAmortization(
            ctx.Contract.Id, userId, ctx.Contract.SpaceId, outcome.AmortizationAmount, effectiveDate, outcome.Strategy,
            outcome.PreviousBalance, outcome.NewBalance, outcome.PreviousTerm, outcome.NewTerm,
            outcome.PreviousPayment, outcome.NewPayment, outcome.EstimatedInterestBefore, outcome.EstimatedInterestAfter,
            outcome.EstimatedSavings, newVersion, idempotencyKey, account?.Id, request.MethodId, request.Note);
        await amortizationRepository.AddAsync(amortization, cancellationToken);

        if (account is not null)
        {
            var transaction = new AccountTransaction(
                account.Id, userId, account.SpaceId, effectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                AccountTransactionKind.Debit, outcome.AmortizationAmount,
                $"Amortização extraordinária - {ctx.Contract.Title}",
                AccountTransactionSourceTypes.LoanPayment, amortization.Id);
            await accountTransactionRepository.AddAsync(transaction, cancellationToken);
            amortization.LinkAccountTransaction(transaction.Id);
        }

        // Regenera as parcelas futuras: remove as em aberto (pagas preservadas) e cria o novo cronograma.
        installmentRepository.RemoveRange(ctx.OpenInstallments);
        var newInstallments = BuildRegeneratedInstallments(ctx, outcome, userId, newVersion);
        if (newInstallments.Count > 0)
            await installmentRepository.AddRangeAsync(newInstallments, cancellationToken);

        if (outcome.Strategy == LoanAmortizationStrategy.FullSettlement || newInstallments.Count == 0)
            ctx.Contract.MarkClosed();
        else
            ctx.Contract.ApplyAmortization(newInstallments.Sum(x => x.TotalAmount), outcome.NewPayment);

        await amortizationRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Loan amortization confirmed {UserId} {ContractId} {AmortizationId} {Strategy}", userId, contractId, amortization.Id, outcome.Strategy);

        var installments = await installmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        return BuildResult(amortization, ctx.Contract, installments, MapSimulation(outcome));
    }

    private List<LoanInstallment> BuildRegeneratedInstallments(AmortizationContext ctx, LoanAmortizationOutcome outcome, Guid userId, int scheduleVersion)
    {
        var lastPaidNo = ctx.PaidInstallments.Count > 0 ? ctx.PaidInstallments.Max(x => x.InstallmentNo) : 0;
        var anchor = ctx.FirstOpenDueDate;
        return outcome.NewSchedule.Rows
            .Select(row => new LoanInstallment(
                ctx.Contract.Id, userId,
                lastPaidNo + row.InstallmentNo,
                NextMonthlyDue(anchor, ctx.Contract.PaymentDay, row.InstallmentNo),
                row.BeginningBalance, row.PrincipalAmount, row.InterestAmount, row.TotalAmount, row.EndingBalance,
                scheduleVersion: scheduleVersion))
            .ToList();
    }

    private async Task<AmortizationContext> LoadContextAsync(Guid userId, Guid contractId, decimal amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new AppProblemException("Valor inválido", "O valor amortizado deve ser maior que zero.", StatusCodes.Status400BadRequest);

        var contract = await contractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        if (contract.Status is LoanStatus.Closed or LoanStatus.Cancelled or LoanStatus.Archived)
            throw new AppProblemException("Contrato não ativo", "O contrato não está ativo e não aceita amortização.", StatusCodes.Status409Conflict, code: "loan_not_active");

        var installments = await installmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        var open = installments
            .Where(x => x.Status is not LoanInstallmentStatus.Paid and not LoanInstallmentStatus.Cancelled)
            .OrderBy(x => x.InstallmentNo)
            .ToList();

        if (open.Count == 0)
            throw new AppProblemException("Sem parcelas em aberto", "O contrato não possui parcelas em aberto para amortizar.", StatusCodes.Status409Conflict, code: "no_open_installments");

        var paid = installments.Where(x => x.Status == LoanInstallmentStatus.Paid).ToList();
        var currentBalance = open[0].BeginningBalance;

        return new AmortizationContext(contract, open, paid, currentBalance, open.Count, contract.MonthlyPayment, contract.MonthlyInterestRate, amount, open.Min(x => x.DueDate));
    }

    private async Task<LoanAmortizationResult> BuildResultForExistingAsync(Guid userId, LoanAmortization amortization, CancellationToken cancellationToken)
    {
        var contract = await contractRepository.GetByIdAsync(amortization.ContractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato da amortização não foi localizado.", StatusCodes.Status404NotFound);
        var installments = await installmentRepository.ListByContractAsync(amortization.ContractId, userId, cancellationToken);
        var simulation = new LoanAmortizationSimulationResult(
            amortization.Strategy, amortization.Amount, amortization.PreviousBalance, amortization.NewBalance,
            amortization.PreviousTerm, amortization.NewTerm, amortization.PreviousPayment, amortization.NewPayment,
            amortization.EstimatedInterestBefore, amortization.EstimatedInterestAfter, amortization.EstimatedSavings, Disclaimer);
        return BuildResult(amortization, contract, installments, simulation);
    }

    private static LoanAmortizationSimulationResult MapSimulation(LoanAmortizationOutcome o) => new(
        o.Strategy, o.AmortizationAmount, o.PreviousBalance, o.NewBalance, o.PreviousTerm, o.NewTerm,
        o.PreviousPayment, o.NewPayment, o.EstimatedInterestBefore, o.EstimatedInterestAfter, o.EstimatedSavings, Disclaimer);

    private static LoanAmortizationResult BuildResult(LoanAmortization amortization, LoanContract contract, IReadOnlyList<LoanInstallment> installments, LoanAmortizationSimulationResult simulation)
    {
        var open = installments
            .Where(x => x.Status is not LoanInstallmentStatus.Paid and not LoanInstallmentStatus.Cancelled)
            .OrderBy(x => x.DueDate)
            .ToList();

        var summary = new LoanContractSummary(
            contract.Id, contract.Status, contract.OpenBalance, contract.PaidAmount, contract.PaidPrincipal,
            contract.PaidInterest, open.Count, open.FirstOrDefault()?.DueDate, contract.MonthlyPayment);

        var installmentResponses = installments
            .OrderBy(x => x.InstallmentNo)
            .Select(x => new LoanInstallmentResponse(
                x.Id, x.InstallmentNo, x.DueDate, x.BeginningBalance, x.PrincipalAmount, x.InterestAmount,
                x.TotalAmount, x.EndingBalance, x.Status, x.PaidAt))
            .ToList();

        return new LoanAmortizationResult(amortization.Id, contract.Id, simulation, amortization.AccountTransactionId, summary, installmentResponses);
    }

    private static DateOnly NextMonthlyDue(DateOnly anchor, int paymentDay, int installmentNo)
    {
        var monthBase = new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(installmentNo - 1);
        var day = Math.Min(paymentDay, DateTime.DaysInMonth(monthBase.Year, monthBase.Month));
        return new DateOnly(monthBase.Year, monthBase.Month, day);
    }

    private sealed record AmortizationContext(
        LoanContract Contract,
        List<LoanInstallment> OpenInstallments,
        List<LoanInstallment> PaidInstallments,
        decimal CurrentBalance,
        int RemainingTerm,
        decimal CurrentPayment,
        decimal MonthlyRate,
        decimal Amount,
        DateOnly FirstOpenDueDate)
    {
        public LoanAmortizationOutcome Simulate(LoanAmortizationStrategy strategy) =>
            LoanCalculator.SimulateExtraordinary(CurrentBalance, MonthlyRate, RemainingTerm, CurrentPayment, Contract.AmortizationType, Amount, strategy);
    }
}
