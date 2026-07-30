using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LoanTimelineServiceTests
{
    [Fact]
    public async Task Timeline_Aggregates_Events_Ordered_By_Most_Recent()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var contract = new LoanContract(userId, spaceId, "Empréstimo", 10000m, 12m, 0.01m, InterestRatePeriod.AnnualNominal, 12,
            LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10, 900m, 12000m, 2000m, 12000m);

        var payment = new LoanPayment(contract.Id, Guid.NewGuid(), userId, spaceId, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), 900m, 800m, 100m, "k1");
        payment.MarkReversed(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), "erro");
        var amortization = new LoanAmortization(contract.Id, userId, spaceId, 2000m, new DateOnly(2026, 3, 1),
            LoanAmortizationStrategy.ReduceTerm, 9000m, 7000m, 11, 8, 900m, 900m, 500m, 300m, 200m, 2, "k2");

        var contracts = new Mock<ILoanContractRepository>();
        contracts.Setup(x => x.GetByIdAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        var payments = new Mock<ILoanPaymentRepository>();
        payments.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync([payment]);
        var amorts = new Mock<ILoanAmortizationRepository>();
        amorts.Setup(x => x.ListByContractAsync(contract.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync([amortization]);

        var sut = new LoanTimelineService(contracts.Object, payments.Object, amorts.Object);

        var events = await sut.GetAsync(userId, contract.Id);

        events.Select(e => e.Type).Should().Contain(new[] { "contract_created", "installment_paid", "payment_reversed", "amortization" });
        // Ordenado do mais recente para o mais antigo.
        events.Select(e => e.At).Should().BeInDescendingOrder();
        events.First().Type.Should().Be("amortization"); // 2026-03-01 é o mais recente
    }
}
