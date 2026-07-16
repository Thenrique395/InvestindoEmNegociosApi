using FluentAssertions;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Tests;

public class MissingSequenceSumCalculatorTests
{
    [Theory]
    [InlineData(new[] { 1, 2, 3, 6, 8 }, 16)]
    [InlineData(new[] { 8, 12 }, 30)]
    [InlineData(new[] { 12, 8 }, 30)]
    public void Calculate_Should_Return_Sum_Of_Missing_Numbers_To_Complete_Sequence(
        int[] numbers,
        long expectedSum)
    {
        var result = MissingSequenceSumCalculator.Calculate(numbers);

        result.Should().Be(expectedSum);
    }

    [Fact]
    public void Calculate_Should_Return_Zero_When_Sequence_Is_Already_Complete()
    {
        var result = MissingSequenceSumCalculator.Calculate(new[] { 4, 5, 6, 7 });

        result.Should().Be(0);
    }

    [Fact]
    public void Calculate_Should_Ignore_Duplicated_Numbers()
    {
        var result = MissingSequenceSumCalculator.Calculate(new[] { 1, 2, 2, 4 });

        result.Should().Be(3);
    }

    [Fact]
    public void Calculate_Should_Return_Zero_When_Input_Has_Less_Than_Two_Unique_Numbers()
    {
        var result = MissingSequenceSumCalculator.Calculate(new[] { 10, 10 });

        result.Should().Be(0);
    }

    [Fact]
    public void Calculate_Should_Throw_When_Input_Is_Null()
    {
        var act = () => MissingSequenceSumCalculator.Calculate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
