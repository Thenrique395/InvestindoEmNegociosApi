using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LoansServiceTests
{
    [Fact]
    public async Task SimulateAsync_Should_Build_Price_Schedule_With_Expected_Length()
    {
        var sut = new LoansService(
            Mock.Of<ILoanContractRepository>(),
            Mock.Of<ILoanInstallmentRepository>(),
            Mock.Of<ISpaceRepository>());

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
        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetDefaultByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Space(userId, "Pessoal", isDefault: true));
        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, spaceRepository.Object);

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
    public async Task PayInstallmentAsync_Should_Throw_AlreadyPaid_When_Concurrent_Write_Wins_The_Race()
    {
        var userId = Guid.NewGuid();
        var contract = new LoanContract(
            userId, Guid.NewGuid(), "Crédito", 5000m, 10m, 0.008333m, InterestRatePeriod.AnnualNominal, 2,
            LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10, 2600m, 5200m, 200m, 5200m);
        var installment = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.GetByIdAsync(installment.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(installment);
        // Simula uma segunda requisição (duplo clique/retry de rede) já tendo salvo a mesma
        // parcela como paga entre a leitura e este SaveChangesAsync — exatamente o que o
        // token de concorrência (Version) detecta de verdade contra um banco real.
        installmentRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        await sut.Invoking(x => x.PayInstallmentAsync(userId, contract.Id, installment.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A parcela já foi paga.");
    }

    [Fact]
    public async Task ListAsync_Should_Map_Open_Balance_From_Open_Installments()
    {
        var userId = Guid.NewGuid();
        var contract = new LoanContract(
            userId,
            Guid.NewGuid(),
            "Crédito",
            5000m,
            10m,
            0.008333m,
            InterestRatePeriod.AnnualNominal,
            2,
            LoanAmortizationType.Price,
            new DateOnly(2026, 1, 10),
            10,
            2600m,
            5200m,
            200m,
            5200m);
        var paid = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);
        paid.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));
        var open = new LoanInstallment(contract.Id, userId, 2, new DateOnly(2026, 2, 10), 2500m, 2500m, 100m, 2600m, 0m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([contract]);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([paid, open]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        var result = await sut.ListAsync(userId);

        result.Should().ContainSingle();
        result[0].OpenInstallments.Should().Be(1);
        result[0].OpenBalance.Should().Be(2600m);
    }

    private static LoanContract BuildContract(Guid userId) => new(
        userId, Guid.NewGuid(), "Crédito", 5000m, 10m, 0.008333m, InterestRatePeriod.AnnualNominal, 2,
        LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10, 2600m, 5200m, 200m, 5200m);

    [Fact]
    public async Task PayInstallmentAsync_Should_Close_Contract_When_Last_Installment_Is_Paid()
    {
        var userId = Guid.NewGuid();
        var contract = BuildContract(userId);
        var alreadyPaid = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);
        alreadyPaid.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));
        var last = new LoanInstallment(contract.Id, userId, 2, new DateOnly(2026, 2, 10), 2500m, 2500m, 100m, 2600m, 0m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.GetByIdAsync(last.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(last);
        // Após pagar a última, a listagem do contrato reflete ambas como pagas (mesmas referências).
        installmentRepository.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alreadyPaid, last]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        await sut.PayInstallmentAsync(userId, contract.Id, last.Id);

        contract.Status.Should().Be(LoanStatus.Closed);
        contract.ClosedAt.Should().NotBeNull();
        contract.OpenBalance.Should().Be(0m);
        contract.PaidAmount.Should().Be(5200m);
        contractRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayInstallmentAsync_Should_Reject_When_Contract_Not_Active()
    {
        var userId = Guid.NewGuid();
        var contract = BuildContract(userId);
        contract.Archive();
        var installment = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.GetByIdAsync(installment.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(installment);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        await sut.Invoking(x => x.PayInstallmentAsync(userId, contract.Id, installment.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_Should_Reject_When_Contract_Has_Paid_Installments()
    {
        var userId = Guid.NewGuid();
        var contract = BuildContract(userId);
        var paid = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);
        paid.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync([paid]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        (await sut.Invoking(x => x.DeleteAsync(userId, contract.Id))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("loan_has_history");
        contractRepository.Verify(x => x.Remove(It.IsAny<LoanContract>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_When_Contract_Has_No_History()
    {
        var userId = Guid.NewGuid();
        var contract = BuildContract(userId);
        var open = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync([open]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        await sut.DeleteAsync(userId, contract.Id);

        contractRepository.Verify(x => x.Remove(contract), Times.Once);
        contractRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_Should_Set_Status_Archived_And_Preserve_History()
    {
        var userId = Guid.NewGuid();
        var contract = BuildContract(userId);
        var paid = new LoanInstallment(contract.Id, userId, 1, new DateOnly(2026, 1, 10), 5000m, 2500m, 100m, 2600m, 2500m);
        paid.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));

        var contractRepository = new Mock<ILoanContractRepository>();
        contractRepository.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var installmentRepository = new Mock<ILoanInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync([paid]);

        var sut = new LoansService(contractRepository.Object, installmentRepository.Object, Mock.Of<ISpaceRepository>());

        var result = await sut.ArchiveAsync(userId, contract.Id);

        contract.Status.Should().Be(LoanStatus.Archived);
        contract.ArchivedAt.Should().NotBeNull();
        result.Status.Should().Be(LoanStatus.Archived);
        contractRepository.Verify(x => x.Remove(It.IsAny<LoanContract>()), Times.Never);
    }

    [Fact]
    public async Task CompareAsync_Returns_Both_Price_And_Sac_For_Same_Inputs()
    {
        var sut = new LoansService(Mock.Of<ILoanContractRepository>(), Mock.Of<ILoanInstallmentRepository>(), Mock.Of<ISpaceRepository>());

        var result = await sut.CompareAsync(Guid.NewGuid(),
            new LoanContractRequest("Comparação", 12000m, 12m, 12, LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10));

        result.Price.AmortizationType.Should().Be(LoanAmortizationType.Price);
        result.Sac.AmortizationType.Should().Be(LoanAmortizationType.Sac);
        result.Price.Installments.Should().HaveCount(12);
        result.Sac.Installments.Should().HaveCount(12);
        // SAC tem parcela decrescente; PRICE ~constante.
        result.Sac.Installments[0].TotalAmount.Should().BeGreaterThan(result.Sac.Installments[11].TotalAmount);
        // SAC amortiza mais cedo → menos juros totais que PRICE.
        result.Sac.TotalInterest.Should().BeLessThan(result.Price.TotalInterest);
    }
}
