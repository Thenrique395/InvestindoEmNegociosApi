using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LoanAmortizationServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    private LoanContract BuildContract(decimal payment)
        => new(_userId, _spaceId, "Empréstimo", 10000m, 12m, 0.01m, InterestRatePeriod.AnnualNominal, 12,
            LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10, payment, 12000m, 2000m, 12000m);

    private List<LoanInstallment> BuildInstallments(Guid contractId, int paidCount)
    {
        var schedule = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price);
        var list = schedule.Rows
            .Select(r => new LoanInstallment(contractId, _userId, r.InstallmentNo,
                new DateOnly(2026, r.InstallmentNo, 10), r.BeginningBalance, r.PrincipalAmount, r.InterestAmount, r.TotalAmount, r.EndingBalance))
            .ToList();
        for (var i = 0; i < paidCount; i++) list[i].MarkPaid(DateTime.UtcNow);
        return list;
    }

    private sealed record Sut(
        LoanAmortizationService Service,
        LoanContract Contract,
        Mock<ILoanInstallmentRepository> Installments,
        Mock<ILoanAmortizationRepository> Amortizations,
        Mock<IAccountTransactionRepository> Transactions);

    private Sut BuildSut(int paidCount = 0)
    {
        var payment = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price).FirstPayment;
        var contract = BuildContract(payment);
        var installments = BuildInstallments(contract.Id, paidCount);

        var contracts = new Mock<ILoanContractRepository>();
        contracts.Setup(x => x.GetByIdAsync(contract.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

        var insts = new Mock<ILoanInstallmentRepository>();
        insts.Setup(x => x.ListByContractAsync(contract.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(installments);

        var amorts = new Mock<ILoanAmortizationRepository>();
        amorts.Setup(x => x.GetByIdempotencyKeyAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LoanAmortization?)null);
        amorts.Setup(x => x.MaxScheduleVersionAsync(contract.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var accounts = new Mock<IAccountRepository>();
        var txns = new Mock<IAccountTransactionRepository>();

        var svc = new LoanAmortizationService(contracts.Object, insts.Object, amorts.Object, accounts.Object, txns.Object, Mock.Of<ILogger<LoanAmortizationService>>());
        return new Sut(svc, contract, insts, amorts, txns);
    }

    [Fact]
    public async Task Simulate_Returns_Estimate_With_Disclaimer()
    {
        var sut = BuildSut();
        var result = await sut.Service.SimulateAsync(_userId, sut.Contract.Id,
            new LoanAmortizationRequest(2000m, LoanAmortizationStrategy.ReduceTerm));

        result.NewBalance.Should().Be(8000m);
        result.EstimatedSavings.Should().BeGreaterThan(0m);
        result.Disclaimer.Should().Contain("instituição financeira");
    }

    [Fact]
    public async Task Confirm_ReduceTerm_Records_And_Regenerates_Schedule()
    {
        var sut = BuildSut();
        List<LoanInstallment>? added = null;
        sut.Installments.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<LoanInstallment>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LoanInstallment>, CancellationToken>((items, _) => added = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await sut.Service.ConfirmAsync(_userId, sut.Contract.Id,
            new LoanAmortizationRequest(2000m, LoanAmortizationStrategy.ReduceTerm));

        sut.Amortizations.Verify(x => x.AddAsync(It.IsAny<LoanAmortization>(), It.IsAny<CancellationToken>()), Times.Once);
        sut.Installments.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<LoanInstallment>>()), Times.Once);
        sut.Amortizations.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        added.Should().NotBeNull();
        added!.Should().OnlyContain(i => i.ScheduleVersion == 2);
        added!.Count.Should().BeLessThan(12, "ReduceTerm encurta o cronograma");
        result.Simulation.Strategy.Should().Be(LoanAmortizationStrategy.ReduceTerm);
        sut.Contract.Status.Should().Be(LoanStatus.Active);
    }

    [Fact]
    public async Task Confirm_FullSettlement_Closes_Contract()
    {
        var sut = BuildSut();
        var result = await sut.Service.ConfirmAsync(_userId, sut.Contract.Id,
            new LoanAmortizationRequest(10000m, LoanAmortizationStrategy.FullSettlement));

        sut.Contract.Status.Should().Be(LoanStatus.Closed);
        sut.Contract.ClosedAt.Should().NotBeNull();
        result.Simulation.NewBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Confirm_Is_Idempotent()
    {
        var sut = BuildSut();
        var existing = new LoanAmortization(sut.Contract.Id, _userId, _spaceId, 2000m, new DateOnly(2026, 2, 1),
            LoanAmortizationStrategy.ReduceTerm, 10000m, 8000m, 12, 9, 888m, 888m, 600m, 400m, 200m, 2, "key-x");
        sut.Amortizations.Setup(x => x.GetByIdempotencyKeyAsync(_userId, "key-x", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await sut.Service.ConfirmAsync(_userId, sut.Contract.Id,
            new LoanAmortizationRequest(2000m, LoanAmortizationStrategy.ReduceTerm, IdempotencyKey: "key-x"));

        result.AmortizationId.Should().Be(existing.Id);
        sut.Amortizations.Verify(x => x.AddAsync(It.IsAny<LoanAmortization>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.Installments.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<LoanInstallment>>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_Rejects_When_Contract_Not_Active()
    {
        var sut = BuildSut();
        sut.Contract.Archive();

        (await sut.Service.Invoking(x => x.ConfirmAsync(_userId, sut.Contract.Id, new LoanAmortizationRequest(2000m, LoanAmortizationStrategy.ReduceTerm)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("loan_not_active");
    }

    [Fact]
    public async Task Confirm_Rejects_When_No_Open_Installments()
    {
        var sut = BuildSut(paidCount: 12);

        (await sut.Service.Invoking(x => x.ConfirmAsync(_userId, sut.Contract.Id, new LoanAmortizationRequest(2000m, LoanAmortizationStrategy.ReduceTerm)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("no_open_installments");
    }
}
