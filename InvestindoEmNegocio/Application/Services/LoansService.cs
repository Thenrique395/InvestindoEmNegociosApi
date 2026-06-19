using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class LoansService(
    ILoanContractRepository loanContractRepository,
    ILoanInstallmentRepository loanInstallmentRepository) : ILoansService
{
    public async Task<IReadOnlyList<LoanContractResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var contracts = await loanContractRepository.ListByUserAsync(userId, cancellationToken);
        var installments = await loanInstallmentRepository.ListByUserAsync(userId, cancellationToken);

        return contracts
            .Select(contract => CreateLoanContractResponse(contract, installments.Where(x => x.ContractId == contract.Id).ToList()))
            .ToList();
    }

    public async Task<LoanContractResponse> CreateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var simulation = BuildSimulation(request);
        var contract = new LoanContract(
            userId,
            request.Title.Trim(),
            request.PrincipalAmount,
            request.AnnualInterestRate,
            request.TermMonths,
            request.AmortizationType,
            request.StartDate,
            request.PaymentDay,
            simulation.MonthlyPayment,
            simulation.TotalCost,
            simulation.TotalInterest);

        await loanContractRepository.AddAsync(contract, cancellationToken);
        var installments = simulation.Installments
            .Select(item => new LoanInstallment(
                contract.Id,
                userId,
                item.InstallmentNo,
                item.DueDate,
                item.BeginningBalance,
                item.PrincipalAmount,
                item.InterestAmount,
                item.TotalAmount,
                item.EndingBalance))
            .ToList();
        await loanInstallmentRepository.AddRangeAsync(installments, cancellationToken);
        await loanContractRepository.SaveChangesAsync(cancellationToken);

        return CreateLoanContractResponse(contract, installments);
    }

    public async Task<LoanContractResponse> UpdateAsync(Guid userId, Guid contractId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        var existing = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        if (existing.Any(x => x.Status == LoanInstallmentStatus.Paid))
            throw new InvalidOperationException("Contratos com parcelas já pagas não podem ser editados.");

        Validate(request);
        var simulation = BuildSimulation(request);
        contract.Update(request.Title.Trim(), request.PrincipalAmount, request.AnnualInterestRate, request.TermMonths,
            request.AmortizationType, request.StartDate, request.PaymentDay,
            simulation.MonthlyPayment, simulation.TotalCost, simulation.TotalInterest);

        await loanInstallmentRepository.RemoveByContractAsync(contractId, userId, cancellationToken);
        var newInstallments = simulation.Installments
            .Select(item => new LoanInstallment(contract.Id, userId, item.InstallmentNo, item.DueDate,
                item.BeginningBalance, item.PrincipalAmount, item.InterestAmount, item.TotalAmount, item.EndingBalance))
            .ToList();
        await loanInstallmentRepository.AddRangeAsync(newInstallments, cancellationToken);
        await loanContractRepository.SaveChangesAsync(cancellationToken);

        return CreateLoanContractResponse(contract, newInstallments);
    }

    public async Task DeleteAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        await loanInstallmentRepository.RemoveByContractAsync(contractId, userId, cancellationToken);
        loanContractRepository.Remove(contract);
        await loanContractRepository.SaveChangesAsync(cancellationToken);
    }

    public Task<LoanSimulationResponse> SimulateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        return Task.FromResult(BuildSimulation(request));
    }

    private static void Validate(LoanContractRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Título do empréstimo é obrigatório.");
        if (request.PrincipalAmount <= 0)
            throw new ArgumentException("Principal deve ser maior que zero.");
        if (request.AnnualInterestRate < 0)
            throw new ArgumentException("Taxa de juros inválida.");
        if (request.TermMonths is < 1 or > 480)
            throw new ArgumentException("Prazo deve ficar entre 1 e 480 meses.");
        if (request.PaymentDay is < 1 or > 28)
            throw new ArgumentException("Dia de pagamento deve ficar entre 1 e 28.");
    }

    private static LoanSimulationResponse BuildSimulation(LoanContractRequest request)
    {
        var monthlyRate = request.AnnualInterestRate / 12m / 100m;
        var installments = new List<LoanInstallmentResponse>(request.TermMonths);
        decimal remaining = request.PrincipalAmount;
        decimal monthlyPayment = 0m;
        decimal totalCost = 0m;
        decimal totalInterest = 0m;

        if (request.AmortizationType == LoanAmortizationType.Price)
        {
            monthlyPayment = monthlyRate <= 0
                ? Math.Round(request.PrincipalAmount / request.TermMonths, 2)
                : Math.Round(request.PrincipalAmount * (monthlyRate / (1 - (decimal)Math.Pow((double)(1 + monthlyRate), -request.TermMonths))), 2);
        }

        var sacPrincipal = request.AmortizationType == LoanAmortizationType.Sac
            ? Math.Round(request.PrincipalAmount / request.TermMonths, 2)
            : 0m;

        for (var index = 1; index <= request.TermMonths; index++)
        {
            var beginning = remaining;
            var interest = Math.Round(beginning * monthlyRate, 2);
            decimal principal;
            decimal installmentValue;

            if (request.AmortizationType == LoanAmortizationType.Sac)
            {
                principal = index == request.TermMonths ? remaining : sacPrincipal;
                installmentValue = Math.Round(principal + interest, 2);
            }
            else
            {
                installmentValue = index == request.TermMonths
                    ? Math.Round(remaining + interest, 2)
                    : monthlyPayment;
                principal = Math.Round(installmentValue - interest, 2);
                if (index == request.TermMonths || principal > remaining)
                    principal = remaining;
            }

            remaining = Math.Max(remaining - principal, 0m);
            totalCost += installmentValue;
            totalInterest += interest;

            installments.Add(new LoanInstallmentResponse(
                Guid.Empty,
                index,
                NextDueDate(request.StartDate, request.PaymentDay, index),
                beginning,
                principal,
                interest,
                installmentValue,
                remaining,
                LoanInstallmentStatus.Open,
                null));
        }

        return new LoanSimulationResponse(
            request.AmortizationType == LoanAmortizationType.Sac
                ? installments.FirstOrDefault()?.TotalAmount ?? 0m
                : monthlyPayment,
            Math.Round(totalCost, 2),
            Math.Round(totalInterest, 2),
            request.AmortizationType,
            installments);
    }

    private static DateOnly NextDueDate(DateOnly startDate, int paymentDay, int installmentNo)
    {
        var monthBase = new DateOnly(startDate.Year, startDate.Month, 1).AddMonths(installmentNo - 1);
        var day = Math.Min(paymentDay, DateTime.DaysInMonth(monthBase.Year, monthBase.Month));
        return new DateOnly(monthBase.Year, monthBase.Month, day);
    }

    private static LoanContractResponse CreateLoanContractResponse(LoanContract contract, List<LoanInstallment> installments)
    {
        var openInstallments = installments.Where(x => x.Status == LoanInstallmentStatus.Open).ToList();
        return new LoanContractResponse(
            contract.Id,
            contract.Title,
            contract.PrincipalAmount,
            contract.AnnualInterestRate,
            contract.TermMonths,
            contract.AmortizationType,
            contract.StartDate,
            contract.PaymentDay,
            contract.MonthlyPayment,
            contract.TotalCost,
            contract.TotalInterest,
            contract.Status,
            openInstallments.Sum(x => x.TotalAmount),
            openInstallments.Count,
            contract.CreatedAt,
            installments
                .OrderBy(x => x.InstallmentNo)
                .Select(x => new LoanInstallmentResponse(
                    x.Id,
                    x.InstallmentNo,
                    x.DueDate,
                    x.BeginningBalance,
                    x.PrincipalAmount,
                    x.InterestAmount,
                    x.TotalAmount,
                    x.EndingBalance,
                    x.Status,
                    x.PaidAt))
                .ToList());
    }
}
