using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PlanHistoryServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SpaceId = Guid.NewGuid();

    [Fact]
    public async Task GetAsync_Should_Return_Null_When_Plan_Is_Not_From_User()
    {
        var sut = BuildSut();

        var result = await sut.GetAsync(UserId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_Derive_Creation_When_There_Is_No_Trail()
    {
        var plan = BuildPlan();
        var sut = BuildSut(plan);

        var result = await sut.GetAsync(UserId, plan.Id);

        result.Should().NotBeNull();
        var criado = result!.Events.Single(e => e.Type == nameof(PlanHistoryEventType.Created));
        criado.Derived.Should().BeTrue("lançamento anterior à trilha só pode ter a criação deduzida");
        criado.OccurredAt.Should().BeCloseTo(plan.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetAsync_Should_Not_Duplicate_Creation_When_It_Was_Recorded()
    {
        var plan = BuildPlan();
        var gravado = new PlanHistoryEntry(
            UserId, SpaceId, plan.Id, PlanHistoryEventType.Created, plan.CreatedAt, actorUserId: UserId);

        var sut = BuildSut(plan, entries: [gravado]);

        var result = await sut.GetAsync(UserId, plan.Id);

        result!.Events.Count(e => e.Type == nameof(PlanHistoryEventType.Created)).Should().Be(1);
        result.Events.Single(e => e.Type == nameof(PlanHistoryEventType.Created)).Derived.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_Should_Derive_Due_Date_Passed_For_Open_Installment_In_The_Past()
    {
        var plan = BuildPlan();
        var vencida = BuildInstallment(plan.Id, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), InstallmentStatus.Open);
        var futura = BuildInstallment(plan.Id, 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), InstallmentStatus.Open);

        var sut = BuildSut(plan, installments: [vencida, futura]);

        var result = await sut.GetAsync(UserId, plan.Id);

        var atrasos = result!.Events.Where(e => e.Type == nameof(PlanHistoryEventType.DueDatePassed)).ToList();
        atrasos.Should().HaveCount(1, "só a parcela em aberto com vencimento no passado gera o evento");
        atrasos[0].InstallmentNo.Should().Be(1);
        atrasos[0].Derived.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_Not_Derive_Due_Date_Passed_For_Paid_Installment()
    {
        var plan = BuildPlan();
        var paga = BuildInstallment(plan.Id, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), InstallmentStatus.Paid);

        var sut = BuildSut(plan, installments: [paga]);

        var result = await sut.GetAsync(UserId, plan.Id);

        result!.Events.Should().NotContain(e => e.Type == nameof(PlanHistoryEventType.DueDatePassed));
    }

    [Fact]
    public async Task GetAsync_Should_Order_Events_From_Oldest_To_Newest()
    {
        var plan = BuildPlan();
        var antigo = new PlanHistoryEntry(
            UserId, SpaceId, plan.Id, PlanHistoryEventType.Created, DateTime.UtcNow.AddDays(-10), actorUserId: UserId);
        var recente = new PlanHistoryEntry(
            UserId, SpaceId, plan.Id, PlanHistoryEventType.AmountChanged, DateTime.UtcNow.AddDays(-1),
            actorUserId: UserId, oldValue: "860.00", newValue: "892.00");

        var sut = BuildSut(plan, entries: [recente, antigo]);

        var result = await sut.GetAsync(UserId, plan.Id);

        result!.Events.Select(e => e.OccurredAt).Should().BeInAscendingOrder();
        var alteracao = result.Events.Single(e => e.Type == nameof(PlanHistoryEventType.AmountChanged));
        alteracao.OldValue.Should().Be("860.00");
        alteracao.NewValue.Should().Be("892.00");
    }

    [Fact]
    public async Task RecordAsync_Should_Never_Persist_Derived_Event()
    {
        var historyRepository = new Mock<IPlanHistoryRepository>();
        var sut = BuildSut(historyRepository: historyRepository);

        await sut.RecordAsync(UserId, Guid.NewGuid(), PlanHistoryEventType.DueDatePassed, DateTime.UtcNow);

        historyRepository.Verify(
            x => x.AddAsync(It.IsAny<PlanHistoryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "vencimento ultrapassado é calculado na leitura; gravar duplicaria o evento");
    }

    private static MoneyPlan BuildPlan() =>
        new(UserId, SpaceId, MoneyType.Expense, "Plano de saúde", 892m, ScheduleType.Recurring,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), FrequencyType.Monthly);

    private static MoneyInstallment BuildInstallment(Guid planId, int no, DateOnly dueDate, InstallmentStatus status)
    {
        var installment = new MoneyInstallment(planId, UserId, SpaceId, no, dueDate, 892m);
        if (status == InstallmentStatus.Paid) installment.RefreshPaymentStatus(892m);
        return installment;
    }

    private static PlanHistoryService BuildSut(
        MoneyPlan? plan = null,
        IReadOnlyList<MoneyInstallment>? installments = null,
        IReadOnlyList<PlanHistoryEntry>? entries = null,
        Mock<IPlanHistoryRepository>? historyRepository = null)
    {
        var history = historyRepository ?? new Mock<IPlanHistoryRepository>();
        history
            .Setup(x => x.ListByPlanAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries ?? []).ToList());

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByPlanAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((installments ?? []).ToList());

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var spaceAccessor = new Mock<ICurrentSpaceAccessor>();
        spaceAccessor.Setup(x => x.RequireSpaceId()).Returns(SpaceId);

        return new PlanHistoryService(
            history.Object,
            Mock.Of<IUserRepository>(),
            planRepository.Object,
            installmentRepository.Object,
            paymentRepository.Object,
            spaceAccessor.Object);
    }
}
