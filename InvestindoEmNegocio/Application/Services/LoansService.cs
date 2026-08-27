using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public class LoansService(
    ILoanContractRepository loanContractRepository,
    ILoanInstallmentRepository loanInstallmentRepository,
    ICurrentSpaceAccessor currentSpaceAccessor) : ILoansService
{
    public async Task<IReadOnlyList<LoanContractResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var contracts = await loanContractRepository.ListByUserAsync(userId, cancellationToken);
        var installments = await loanInstallmentRepository.ListByUserAsync(userId, cancellationToken);

        return contracts
            .Select(contract => CreateLoanContractResponse(contract, installments.Where(x => x.ContractId == contract.Id).ToList()))
            .ToList();
    }

    public async Task<LoanContractResponse> GetAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);
        var installments = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        return CreateLoanContractResponse(contract, installments);
    }

    public async Task<LoanContractResponse> CreateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        // Entidade "dona": recebe o espaço ATIVO da sessão, como as demais entidades
        // financeiras (BACKEND_PADROES_IMPLEMENTACAO.md, Multi-tenancy). Antes usava o espaço
        // padrão do usuário — o contrato criado dentro de uma área nascia marcado com outra.
        var spaceId = currentSpaceAccessor.RequireSpaceId();

        var simulation = BuildSimulation(request);
        var openBalance = simulation.Installments.Sum(x => x.TotalAmount);
        var contract = new LoanContract(
            userId,
            spaceId,
            request.Title.Trim(),
            request.PrincipalAmount,
            request.AnnualInterestRate,
            LoanCalculator.MonthlyRateFromAnnualNominal(request.AnnualInterestRate),
            InterestRatePeriod.AnnualNominal,
            request.TermMonths,
            request.AmortizationType,
            request.StartDate,
            request.PaymentDay,
            simulation.MonthlyPayment,
            simulation.TotalCost,
            simulation.TotalInterest,
            openBalance);

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
        var openBalance = simulation.Installments.Sum(x => x.TotalAmount);
        contract.Update(request.Title.Trim(), request.PrincipalAmount, request.AnnualInterestRate,
            LoanCalculator.MonthlyRateFromAnnualNominal(request.AnnualInterestRate), InterestRatePeriod.AnnualNominal,
            request.TermMonths, request.AmortizationType, request.StartDate, request.PaymentDay,
            simulation.MonthlyPayment, simulation.TotalCost, simulation.TotalInterest, openBalance);

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

        // Regra de domínio: contrato COM histórico (parcelas pagas) não pode ser excluído
        // fisicamente — deve ser arquivado, preservando parcelas, pagamentos e documentos.
        var installments = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        if (installments.Any(x => x.Status == LoanInstallmentStatus.Paid))
            throw new AppProblemException(
                "Exclusão não permitida",
                "Este contrato possui parcelas pagas. Arquive o contrato para preservar o histórico.",
                StatusCodes.Status409Conflict,
                code: "loan_has_history");

        // Sem histórico: exclusão física. As parcelas são removidas pela FK ON DELETE CASCADE
        // (relação modelada em LoanInstallmentConfiguration). Não apagar explicitamente aqui:
        // isso corria com a cascata do banco e gerava DbUpdateConcurrencyException (Version) → 500.
        loanContractRepository.Remove(contract);
        await loanContractRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoanContractResponse> ArchiveAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        contract.Archive();
        await loanContractRepository.SaveChangesAsync(cancellationToken);

        var installments = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        return CreateLoanContractResponse(contract, installments.ToList());
    }

    public async Task<LoanContractResponse> CancelAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        contract.Cancel();
        await loanContractRepository.SaveChangesAsync(cancellationToken);

        var installments = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        return CreateLoanContractResponse(contract, installments.ToList());
    }

    public async Task<LoanInstallmentResponse> PayInstallmentAsync(Guid userId, Guid contractId, Guid installmentId, CancellationToken cancellationToken = default)
    {
        var contract = await loanContractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        var installment = await loanInstallmentRepository.GetByIdAsync(installmentId, userId, cancellationToken)
            ?? throw new AppProblemException("Parcela não encontrada", "A parcela informada não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        if (installment.ContractId != contract.Id)
            throw new AppProblemException("Parcela inválida", "A parcela não pertence ao contrato informado.", StatusCodes.Status400BadRequest);

        if (contract.Status is LoanStatus.Closed or LoanStatus.Cancelled or LoanStatus.Archived)
            throw new InvalidOperationException("O contrato não está ativo e não aceita novos pagamentos.");

        if (installment.Status == LoanInstallmentStatus.Paid)
            throw new InvalidOperationException("A parcela já foi paga.");

        installment.MarkPaid(DateTime.UtcNow);
        try
        {
            await loanInstallmentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("A parcela já foi paga.");
        }

        // Acompanhamento + quitação automática: recomputa a partir das parcelas do contrato.
        var installments = await loanInstallmentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        var paid = installments.Where(x => x.Status == LoanInstallmentStatus.Paid).ToList();
        var open = installments
            .Where(x => x.Status is not LoanInstallmentStatus.Paid and not LoanInstallmentStatus.Cancelled)
            .ToList();

        contract.UpdateTracking(
            open.Sum(x => x.TotalAmount),
            paid.Sum(x => x.TotalAmount),
            paid.Sum(x => x.PrincipalAmount),
            paid.Sum(x => x.InterestAmount));

        // Última parcela paga → contrato quitado automaticamente (saldo zero).
        if (open.Count == 0)
            contract.MarkClosed();

        await loanContractRepository.SaveChangesAsync(cancellationToken);

        return new LoanInstallmentResponse(
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
    }

    public Task<LoanSimulationResponse> SimulateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        return Task.FromResult(BuildSimulation(request));
    }

    public Task<LoanSimulationComparison> CompareAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var price = BuildSimulation(request with { AmortizationType = LoanAmortizationType.Price });
        var sac = BuildSimulation(request with { AmortizationType = LoanAmortizationType.Sac });
        return Task.FromResult(new LoanSimulationComparison(price, sac));
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
        // Fonte oficial dos cálculos: LoanCalculator (domínio puro, arredondamento half-up,
        // saldo final zerado, resíduo na última parcela). A taxa informada é tratada como
        // anual nominal → mensal linear, preservando a convenção atual da API.
        var monthlyRate = LoanCalculator.MonthlyRateFromAnnualNominal(request.AnnualInterestRate);
        var schedule = LoanCalculator.Build(
            request.PrincipalAmount,
            monthlyRate,
            request.TermMonths,
            request.AmortizationType);

        var installments = schedule.Rows
            .Select(row => new LoanInstallmentResponse(
                Guid.Empty,
                row.InstallmentNo,
                NextDueDate(request.StartDate, request.PaymentDay, row.InstallmentNo),
                row.BeginningBalance,
                row.PrincipalAmount,
                row.InterestAmount,
                row.TotalAmount,
                row.EndingBalance,
                LoanInstallmentStatus.Open,
                null))
            .ToList();

        return new LoanSimulationResponse(
            schedule.FirstPayment,
            schedule.TotalCost,
            schedule.TotalInterest,
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
