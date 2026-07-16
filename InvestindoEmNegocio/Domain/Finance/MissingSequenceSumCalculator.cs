namespace InvestindoEmNegocio.Domain.Finance;

public static class MissingSequenceSumCalculator
{
    public static long Calculate(int[] numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);

        var uniqueNumbers = numbers.ToHashSet();

        if (uniqueNumbers.Count <= 1)
        {
            return 0;
        }

        var min = uniqueNumbers.Min();
        var max = uniqueNumbers.Max();
        var sum = 0L;

        for (var current = min; current <= max; current++)
        {
            if (!uniqueNumbers.Contains(current))
            {
                sum += current;
            }
        }

        return sum;
    }
}
