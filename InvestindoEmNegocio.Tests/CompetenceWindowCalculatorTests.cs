using FluentAssertions;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Tests;

public class CompetenceWindowCalculatorTests
{
    [Fact]
    public void Resolve_Should_Adjust_To_Last_Day_For_February_When_CarryOverDay_Is_31()
    {
        var reference = new DateOnly(2026, 2, 20);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 31);

        start.Should().Be(new DateOnly(2026, 1, 31));
        end.Should().Be(new DateOnly(2026, 2, 27));
    }

    [Fact]
    public void Resolve_Should_Use_30_For_April_When_CarryOverDay_Is_31()
    {
        var reference = new DateOnly(2026, 4, 30);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 31);

        start.Should().Be(new DateOnly(2026, 4, 30));
        end.Should().Be(new DateOnly(2026, 5, 30));
    }

    [Fact]
    public void Resolve_Should_Keep_31_In_Months_With_31_Days()
    {
        var reference = new DateOnly(2026, 3, 31);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 31);

        start.Should().Be(new DateOnly(2026, 3, 31));
        end.Should().Be(new DateOnly(2026, 4, 29));
    }

    [Theory]
    [InlineData(0, 2026, 2, 1)]
    [InlineData(40, 2026, 2, 28)]
    public void BuildSafeDate_Should_Clamp_Invalid_Days(int day, int year, int month, int expectedDay)
    {
        var date = CompetenceWindowCalculator.BuildSafeDate(year, month, day);
        date.Should().Be(new DateOnly(year, month, expectedDay));
    }
}

