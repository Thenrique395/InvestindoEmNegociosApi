using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LoansServiceTests
{
    [Fact]
    public async Task SimulateAsync_Should_Build_Price_Schedule_With_Expected_Length()
    {
        var sut = new LoansService(
            Mock.Of<ILoanContractRepository>(),
            Mock.Of<ILoanInstallmentRepository>());

        var result = await sut.SimulateAsync(
            Guid.NewGuid(),
            new LoanContractRequest(
                "Empréstimo pessoal",
                12000m,
                12m,
                12,
                LoanAmortizationType.Price,
                new DateOnly(2026, 3, 10),
                10));

        result.AmortizationType.Should().Be(LoanAmortizationType.Price);
        result.Installments.Should().HaveCount(12);
        result.MonthlyPayment.Should().BePositive();
        result.TotalCost.Should().BeGreaterThan(12000m);
        result.Installments[0].DueDate.Should().Be(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Contract_And_Installments()
    {
        var userId = Guid.NewGuid();
        var contractRepository = new Mock<ILoanContractRepository>();
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        var sut = new LoansService(contractRepository.Object, installmentRepository.Object);

        var result = await sut.CreateAsync(
            userId,
            new LoanContractRequest(
                "Financiamento",
                10000m,
                18m,
                6,
                LoanAmortizationType.Sac,
                new DateOnly(2026, 4, 5),
                5));

        contractRepository.Verify(x => x.AddAsync(It.Is<LoanContract>(c =>
            c.UserId == userId &&
            c.Title == "Financiamento" &&
            c.TermMonths == 6), It.IsAny<CancellationToken>()), Times.Once);
        installmentRepository.Verify(x => x.AddRangeAsync(
            It.Is<IReadOnlyCollection<LoanInstallment>>(items => items.Count == 6),
            It.IsAny<CancellationToken>()), Times.Once);
        contractRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.Installments.Should().HaveCount(6);
        result.OpenInstallments.Should().Be(6);
    }

    [Fact]
    public async Task ListAsync_Should_Map_Open_Balance_From_Open_Installments()
    {
        var userId = Guid.NewGuid();
        var contract = new LoanContract(
            userId,
            "Crédito",
            5000m,
            10m,
            2,
            LoanAmortizationType.Price,
            new DateOnly(2026, 1, 10),
            10,
            2600m,
            5200m,
            200m);
        var paid = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);
        paid.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));
        var open = new LoanInstallment(contract.Id, userId, 2, new DateOnly(2026, 2, 10), 2500m, 2500m, 100m, 2600m, 0m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([contract]);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([paid, open]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object);

        var result = await sut.ListAsync(userId);

        result.Should().ContainSingle();
        result[0].OpenInstallments.Should().Be(1);
        result[0].OpenBalance.Should().Be(2600m);
    }
}
