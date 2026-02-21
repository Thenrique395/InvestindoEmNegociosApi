using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Tests;

public class NotificationSettingsDtoTests
{
    [Fact]
    public void NotificationSettingsDto_Should_Expose_All_Values()
    {
        var dto = new NotificationSettingsDto(
            true, 2,
            true, 3,
            true,
            true, 4,
            true,
            true,
            false,
            true,
            true,
            false,
            45);

        dto.IncomeUpcomingEnabled.Should().BeTrue();
        dto.IncomeDaysBefore.Should().Be(2);
        dto.ExpenseDaysBefore.Should().Be(3);
        dto.CardCloseDaysBefore.Should().Be(4);
        dto.GoalInactivityEnabled.Should().BeFalse();
        dto.GoalInactivityDays.Should().Be(45);
    }

    [Fact]
    public void UpdateNotificationSettingsRequest_Should_Expose_All_Values()
    {
        var request = new UpdateNotificationSettingsRequest(
            false, 1,
            false, 1,
            true,
            false, 2,
            true,
            false,
            true,
            false,
            true,
            true,
            10);

        request.IncomeUpcomingEnabled.Should().BeFalse();
        request.ExpenseUpcomingEnabled.Should().BeFalse();
        request.ExpenseOverdueEnabled.Should().BeTrue();
        request.CardCloseSoonEnabled.Should().BeFalse();
        request.MonthSummaryEnabled.Should().BeTrue();
        request.GoalCompletedEnabled.Should().BeTrue();
        request.GoalInactivityDays.Should().Be(10);
    }
}
