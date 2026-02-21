using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
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
            It.Is<IEnumerable<MoneyInstallment>>(list => list.Count() == 6),
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

    private static PlansService BuildSut(
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null)
    {
        return new PlansService(
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            NullLogger<PlansService>.Instance);
    }
}
