namespace InvestindoEmNegocio.Domain.Entities;

public class Card
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public int BrandId { get; private set; }
    public string? Bank { get; private set; }
    public decimal CreditLimit { get; private set; }
    public int StatementCloseDay { get; private set; }
    public int DueDay { get; private set; }
    public string HolderName { get; private set; } = string.Empty;
    public string Nickname { get; private set; } = string.Empty;
    public string Last4 { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private Card() { }

    public Card(Guid userId, int brandId, string holderName, string nickname, string last4, string? bank, decimal creditLimit, int statementCloseDay, int dueDay)
    {
        UserId = userId;
        BrandId = brandId;
        Bank = NormalizeOptional(bank);
        CreditLimit = NormalizeCreditLimit(creditLimit);
        StatementCloseDay = NormalizeDay(statementCloseDay, nameof(statementCloseDay));
        DueDay = NormalizeDay(dueDay, nameof(dueDay));
        HolderName = holderName.Trim();
        Nickname = string.IsNullOrWhiteSpace(nickname) ? HolderName : nickname.Trim();
        Last4 = NormalizeLast4(last4);
    }

    public void Update(int brandId, string holderName, string nickname, string last4, string? bank, decimal creditLimit, int statementCloseDay, int dueDay)
    {
        BrandId = brandId;
        Bank = NormalizeOptional(bank);
        CreditLimit = NormalizeCreditLimit(creditLimit);
        StatementCloseDay = NormalizeDay(statementCloseDay, nameof(statementCloseDay));
        DueDay = NormalizeDay(dueDay, nameof(dueDay));
        HolderName = holderName.Trim();
        Nickname = string.IsNullOrWhiteSpace(nickname) ? HolderName : nickname.Trim();
        Last4 = NormalizeLast4(last4);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeLast4(string last4)
    {
        var digits = new string((last4 ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 4) throw new ArgumentException("last4 precisa ter 4 dígitos.");
        return digits;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static decimal NormalizeCreditLimit(decimal creditLimit)
    {
        if (creditLimit < 0) throw new ArgumentException("Limite de crédito não pode ser negativo.");
        return creditLimit;
    }

    private static int NormalizeDay(int day, string field)
    {
        if (day < 1 || day > 31) throw new ArgumentException($"{field} deve estar entre 1 e 31.");
        return day;
    }
}
