using FluentAssertions;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class FinanceRulesSmokeTests
{
    [Fact]
    public void CompetenceWindow_Should_Clamp_Month_Day_And_Preserve_Window()
    {
        var (start, end) = CompetenceWindowCalculator.Resolve(new DateOnly(2026, 2, 20), 31);

        start.Should().Be(new DateOnly(2026, 1, 31));
        end.Should().Be(new DateOnly(2026, 2, 27));
    }

    [Theory]
    [InlineData(1000, 200, 800)]
    [InlineData(1000, 1000, 0)]
    [InlineData(1000, 1200, 0)]
    public void CardStatementConsolidation_Should_Never_Return_Negative_Open_Amount(decimal amount, decimal paid, decimal expectedOpen)
    {
        CardStatementConsolidationEngine.NormalizeOpenAmount(amount, paid)
            .Should().Be(expectedOpen);
    }
}
