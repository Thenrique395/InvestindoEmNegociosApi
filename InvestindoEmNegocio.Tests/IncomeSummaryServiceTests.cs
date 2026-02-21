using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Enums;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class IncomeSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_Should_Aggregate_Current_Month_Values()
    {
        var userId = Guid.NewGuid();
        var targetMonth = "2026-02";
        var plansService = new Mock<IPlansService>();
        var installmentsService = new Mock<IInstallmentsService>();

        var recurringPlanId = Guid.NewGuid();
        var oneTimePlanId = Guid.NewGuid();
        plansService
            .Setup(x => x.ListAsync(userId, MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlanResponse>
            {
                new(recurringPlanId, MoneyType.Income, "Salário", 5000, ScheduleType.Recurring, FrequencyType.Monthly, null, new DateOnly(2025, 1, 1), "Active", null, null),
                new(oneTimePlanId, MoneyType.Income, "Bônus", 1000, ScheduleType.OneTime, null, 1, new DateOnly(2026, 2, 10), "Active", null, null)
            });
        installmentsService
            .Setup(x => x.ListAsync(userId, null, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstallmentResponse>
            {
                new(Guid.NewGuid(), recurringPlanId, 1, new DateOnly(2026, 2, 5), 5000, InstallmentStatus.Paid),
                new(Guid.NewGuid(), oneTimePlanId, 1, new DateOnly(2026, 2, 10), 1000, InstallmentStatus.Paid)
            });

        var sut = new IncomeSummaryService(plansService.Object, installmentsService.Object);

        var result = await sut.GetSummaryAsync(userId, targetMonth);

        result.Month.Should().Be("2026-02");
        result.Total.Should().Be(6000);
        result.TotalRecurring.Should().Be(5000);
        result.TotalOneTime.Should().Be(1000);
        result.Items.Should().HaveCount(2);
        result.History.Should().HaveCount(12);
    }
}
