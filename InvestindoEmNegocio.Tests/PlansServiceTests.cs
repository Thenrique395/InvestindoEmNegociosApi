using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PlansServiceTests
{
    [Fact]
    public async Task CreateAsync_Should_Throw_When_OneTime_Has_Invalid_InstallmentsCount()
    {
        var sut = BuildSut();

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano teste",
            100,
            ScheduleType.OneTime,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Frequency: null,
            InstallmentsCount: 2);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ONE_TIME*");
    }

    [Fact]
    public async Task CreateAsync_Should_Generate_Recurring_Installments()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var sut = BuildSut(installmentRepository: installmentRepository);

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano recorrente",
            100,
            ScheduleType.Recurring,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            FrequencyType.Monthly,
            InstallmentsCount: null);

        var result = await sut.CreateAsync(Guid.NewGuid(), request);

        result.Title.Should().Be("Plano recorrente");
        installmentRepository.Verify(x => x.AddRangeAsync(
            It.Is<IEnumerable<MoneyInstallment>>(list => list.Count() == PlansService.RecurringHorizonMonths),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Plan_Not_Found()
    {
        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyPlan?)null);
        var sut = BuildSut(planRepository: planRepository);

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano",
            100,
            ScheduleType.OneTime,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Frequency: null,
            InstallmentsCount: 1);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Recurring_Has_InstallmentsCount()
    {
        var sut = BuildSut();

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano recorrente inválido",
            100,
            ScheduleType.Recurring,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            FrequencyType.Monthly,
            InstallmentsCount: 2);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RECURRING não aceita installmentsCount*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Installments_Has_Invalid_Count()
    {
        var sut = BuildSut();

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano parcelado inválido",
            100,
            ScheduleType.Installments,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Frequency: null,
            InstallmentsCount: 1);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*INSTALLMENTS requer installmentsCount >= 2*");
    }

    [Fact]
    public async Task CreateAsync_Should_Generate_OneTime_Installment()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var sut = BuildSut(installmentRepository: installmentRepository);

        var request = new CreatePlanRequest(
            MoneyType.Income,
            "Plano único",
            350,
            ScheduleType.OneTime,
            new DateOnly(2026, 2, 21),
            Frequency: null,
            InstallmentsCount: 1);

        var result = await sut.CreateAsync(Guid.NewGuid(), request);

        result.Schedule.Should().Be(ScheduleType.OneTime);
        installmentRepository.Verify(x => x.AddAsync(
            It.Is<MoneyInstallment>(i => i.InstallmentNo == 1 && i.Amount == 350m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Generate_Installments_Plan()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var sut = BuildSut(installmentRepository: installmentRepository);

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano parcelado",
            200,
            ScheduleType.Installments,
            new DateOnly(2026, 1, 10),
            Frequency: null,
            InstallmentsCount: 3);

        var result = await sut.CreateAsync(Guid.NewGuid(), request);

        result.Schedule.Should().Be(ScheduleType.Installments);
        installmentRepository.Verify(x => x.AddRangeAsync(
            It.Is<IEnumerable<MoneyInstallment>>(items =>
                items.Count() == 3 &&
                items.First().InstallmentNo == 1 &&
                items.Last().InstallmentNo == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Compute_Card_Statement_Competence_When_Card_Is_Informed()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Card(
                userId,
                spaceId,
                brandId: 1,
                holderName: "Teste",
                nickname: "Meu cartao",
                last4: "1234",
                bank: "Banco X",
                creditLimit: 1000m,
                statementCloseDay: 8,
                dueDay: 15));

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var sut = BuildSut(installmentRepository: installmentRepository, cardRepository: cardRepository, spaceId: spaceId);

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Compra cartao",
            250m,
            ScheduleType.OneTime,
            new DateOnly(2026, 2, 9),
            Frequency: null,
            InstallmentsCount: 1,
            CardId: cardId);

        await sut.CreateAsync(userId, request);

        installmentRepository.Verify(x => x.AddAsync(
            It.Is<MoneyInstallment>(i =>
                i.StatementYear == 2026 &&
                i.StatementMonth == 3 &&
                i.StatementCloseDate == new DateOnly(2026, 3, 8) &&
                i.StatementDueDate == new DateOnly(2026, 3, 15) &&
                i.DueDate == new DateOnly(2026, 3, 15)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Installments_Has_Frequency()
    {
        var sut = BuildSut();
        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Inválido",
            100,
            ScheduleType.Installments,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            FrequencyType.Monthly,
            InstallmentsCount: 2);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*INSTALLMENTS não aceita frequency*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_OneTime_Has_Frequency()
    {
        var sut = BuildSut();
        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Inválido",
            100,
            ScheduleType.OneTime,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            FrequencyType.Monthly,
            InstallmentsCount: 1);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ONE_TIME não aceita frequency*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Recurring_Has_No_Frequency()
    {
        var sut = BuildSut();
        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Inválido",
            100,
            ScheduleType.Recurring,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Frequency: null,
            InstallmentsCount: null);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RECURRING requer frequency*");
    }

    [Fact]
    public async Task ListAsync_Should_Return_Mapped_Plans()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var planRepository = new Mock<IMoneyPlanRepository>();
        var plans = new List<MoneyPlan>
        {
            new(userId, spaceId, MoneyType.Expense, "Plano A", 100, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, null),
            new(userId, spaceId, MoneyType.Income, "Plano B", 200, ScheduleType.Recurring, new DateOnly(2026, 1, 1), FrequencyType.Monthly, null, null, null, null)
        };

        planRepository.Setup(x => x.ListByUserAsync(userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(plans);
        var sut = BuildSut(planRepository: planRepository);

        var result = await sut.ListAsync(userId, null);

        result.Should().HaveCount(2);
        result.Select(x => x.Title).Should().Contain(["Plano A", "Plano B"]);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Plan_Does_Not_Exist()
    {
        var userId = Guid.NewGuid();
        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>())).ReturnsAsync((MoneyPlan?)null);
        var sut = BuildSut(planRepository: planRepository);

        var result = await sut.GetByIdAsync(userId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Details_With_Installments()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Plano", 100, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, null);
        var planId = plan.Id;

        var installments = new List<MoneyInstallment>
        {
            new(planId, userId, spaceId, 1, new DateOnly(2026, 1, 10), 100)
        };

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.GetByIdAsync(planId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByPlanAsync(planId, userId, It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync(installments);

        var sut = BuildSut(planRepository: planRepository, installmentRepository: installmentRepository);
        var result = await sut.GetByIdAsync(userId, planId);

        result.Should().NotBeNull();
        result!.Installments.Should().HaveCount(1);
        result.Plan.Title.Should().Be("Plano");
    }

    [Fact]
    public async Task UpdateAsync_Should_Regenerate_Installments_And_Remove_Old_Payments()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Plano", 100, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, null);
        var planId = plan.Id;

        var oldInstallments = new List<MoneyInstallment>
        {
            new(planId, userId, spaceId, 1, new DateOnly(2026, 1, 10), 100),
            new(planId, userId, spaceId, 2, new DateOnly(2026, 2, 10), 100)
        };

        var oldPayments = new List<MoneyPayment>
        {
            new(oldInstallments[0].Id, userId, spaceId, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100, 1, "ok")
        };

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.GetByIdAsync(planId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByPlanAsync(planId, userId, It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync(oldInstallments);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldPayments);
        var accountTransactionRepository = new Mock<IAccountTransactionRepository>();
        accountTransactionRepository
            .Setup(x => x.ListBySourceAsync(userId, "InstallmentPayment", It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AccountTransaction(
                    Guid.NewGuid(),
                    userId,
                    spaceId,
                    DateTime.UtcNow,
                    AccountTransactionKind.Debit,
                    100m,
                    "Pagamento parcela 1 - Plano",
                    "InstallmentPayment",
                    oldPayments[0].Id)
            ]);

        var sut = BuildSut(planRepository, installmentRepository, paymentRepository, accountTransactionRepository: accountTransactionRepository, spaceId: spaceId);

        var request = new CreatePlanRequest(
            MoneyType.Expense,
            "Plano atualizado",
            150,
            ScheduleType.Installments,
            new DateOnly(2026, 3, 1),
            Frequency: null,
            InstallmentsCount: 3);

        var result = await sut.UpdateAsync(userId, planId, request);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Plano atualizado");
        paymentRepository.Verify(x => x.RemoveRange(oldPayments), Times.Once);
        accountTransactionRepository.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<AccountTransaction>>()), Times.Once);
        installmentRepository.Verify(x => x.RemoveRange(oldInstallments), Times.Once);
        installmentRepository.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<MoneyInstallment>>(l => l.Count() == 3), It.IsAny<CancellationToken>()), Times.Once);
        planRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_MarkDeleted_On_Plan_When_Found()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Plano", 100, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, null);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var sut = BuildSut(planRepository: planRepository, spaceId: spaceId);

        var result = await sut.DeleteAsync(userId, Guid.NewGuid());

        result.Should().BeTrue();
        plan.DeletedAt.Should().NotBeNull();
        planRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_Cascade_MarkDeleted_To_Installments_Payments_And_Transactions()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Plano", 100, ScheduleType.Installments, new DateOnly(2026, 1, 1), null, 2, null, null, null);
        var installment = new MoneyInstallment(plan.Id, userId, spaceId, 1, new DateOnly(2026, 2, 1), 50);
        var payment = new MoneyPayment(installment.Id, userId, spaceId, DateTime.UtcNow, 50);
        var transaction = new AccountTransaction(Guid.NewGuid(), userId, spaceId, DateTime.UtcNow, AccountTransactionKind.Debit, 50, "Parcela", AccountTransactionSourceTypes.InstallmentPayment, payment.Id);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.GetByIdAsync(plan.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByPlanAsync(plan.Id, userId, It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync([installment]);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository.Setup(x => x.ListByInstallmentIdsAsync(It.Is<List<Guid>>(l => l.Contains(installment.Id)), It.IsAny<CancellationToken>())).ReturnsAsync([payment]);

        var accountTransactionRepository = new Mock<IAccountTransactionRepository>();
        accountTransactionRepository
            .Setup(x => x.ListBySourceAsync(userId, AccountTransactionSourceTypes.InstallmentPayment, It.Is<List<Guid>>(l => l.Contains(payment.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transaction]);

        var sut = BuildSut(
            planRepository: planRepository,
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            accountTransactionRepository: accountTransactionRepository,
            spaceId: spaceId);

        var result = await sut.DeleteAsync(userId, plan.Id);

        result.Should().BeTrue();
        plan.DeletedAt.Should().NotBeNull();
        installment.DeletedAt.Should().NotBeNull();
        payment.DeletedAt.Should().NotBeNull();
        transaction.DeletedAt.Should().NotBeNull();
    }

    private static PlansService BuildSut(
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IAccountTransactionRepository>? accountTransactionRepository = null,
        Mock<ICardRepository>? cardRepository = null,
        Guid? spaceId = null)
    {
        var currentSpaceAccessor = new Mock<ICurrentSpaceAccessor>();
        var resolvedSpaceId = spaceId ?? Guid.NewGuid();
        currentSpaceAccessor.Setup(x => x.SpaceId).Returns(resolvedSpaceId);
        currentSpaceAccessor.Setup(x => x.RequireSpaceId()).Returns(resolvedSpaceId);

        return new PlansService(
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            accountTransactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            currentSpaceAccessor.Object,
            NullLogger<PlansService>.Instance);
    }
}
