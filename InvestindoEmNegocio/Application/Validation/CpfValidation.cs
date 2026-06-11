namespace InvestindoEmNegocio.Application.Validation;

internal static class CpfValidation
{
    public static string Normalize(string document)
    {
        return new string((document ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    public static bool IsValid(string document)
    {
        var digits = Normalize(document);
        if (digits.Length != 11) return false;
        if (digits.Distinct().Count() == 1) return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        var firstCheck = CalculateVerifierDigit(numbers, 9, 10);
        if (firstCheck != numbers[9]) return false;

        var secondCheck = CalculateVerifierDigit(numbers, 10, 11);
        return secondCheck == numbers[10];
    }

    private static int CalculateVerifierDigit(int[] numbers, int length, int firstWeight)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += numbers[i] * (firstWeight - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
