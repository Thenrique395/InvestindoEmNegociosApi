using FluentAssertions;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Tests;

public class CompetenceWindowCalculatorEdgeCaseTests
{
    [Fact]
    public void Resolve_Should_Handle_Year_Boundary_When_Reference_Is_Before_CarryOverDay()
    {
        var reference = new DateOnly(2026, 1, 2);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 5);

        start.Should().Be(new DateOnly(2025, 12, 5));
        end.Should().Be(new DateOnly(2026, 1, 4));
    }

    [Fact]
    public void Resolve_Should_Clamp_To_Feb29_On_Leap_Year_When_CarryOverDay_Is_31()
    {
        var reference = new DateOnly(2024, 2, 29);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 31);

        start.Should().Be(new DateOnly(2024, 2, 29));
        end.Should().Be(new DateOnly(2024, 3, 30));
    }

    [Fact]
    public void Resolve_Should_Keep_Full_Month_Window_When_CarryOverDay_Is_1()
    {
        var reference = new DateOnly(2026, 8, 20);

        var (start, end) = CompetenceWindowCalculator.Resolve(reference, 1);

        start.Should().Be(new DateOnly(2026, 8, 1));
        end.Should().Be(new DateOnly(2026, 8, 31));
    }
}
