using System.Linq;

namespace InvestindoEmNegocio.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime? BirthDate { get; private set; }
    public int CarryOverDay { get; private set; } = 1;
    public string FinancialGoal { get; private set; } = string.Empty;
    public string IntelligenceMode { get; private set; } = "B";
    public string Language { get; private set; } = "pt-BR";
    public string Currency { get; private set; } = "BRL";
    public bool NotifyUpcomingEnabled { get; private set; } = true;
    public bool NotifyOverdueEnabled { get; private set; } = true;
    public bool NotifyEmailEnabled { get; private set; } = false;
    public bool NotifyInAppEnabled { get; private set; } = true;
    public int NotifyDaysBeforeDue { get; private set; } = 3;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserProfile() { }

    public UserProfile(
        Guid userId,
        string fullName,
        string phone,
        DateTime? birthDate,
        string language = "pt-BR",
        string currency = "BRL",
        int carryOverDay = 1,
        string financialGoal = "",
        string intelligenceMode = "B")
    {
        UserId = userId;
        UpdateProfileData(fullName, phone, birthDate, language, currency, carryOverDay, financialGoal, intelligenceMode);
    }

    public void UpdateProfileData(
        string fullName,
        string phone,
        DateTime? birthDate,
        string language = "pt-BR",
        string currency = "BRL",
        int carryOverDay = 1,
        string financialGoal = "",
        string intelligenceMode = "B")
    {
        FullName = fullName.Trim();
        Phone = SanitizePhone(phone);
        BirthDate = NormalizeUtcDate(birthDate);
        CarryOverDay = NormalizeCarryOverDay(carryOverDay);
        FinancialGoal = financialGoal?.Trim() ?? string.Empty;
        IntelligenceMode = NormalizeIntelligenceMode(intelligenceMode);
        Language = string.IsNullOrWhiteSpace(language) ? "pt-BR" : language.Trim();
        Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetNotificationPreferences(bool upcomingEnabled, bool overdueEnabled, bool emailEnabled, bool inAppEnabled, int daysBeforeDue)
    {
        NotifyUpcomingEnabled = upcomingEnabled;
        NotifyOverdueEnabled = overdueEnabled;
        NotifyEmailEnabled = emailEnabled;
        NotifyInAppEnabled = inAppEnabled;
        NotifyDaysBeforeDue = Math.Max(0, daysBeforeDue);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePreferenceSettings(string language, string currency)
    {
        Language = string.IsNullOrWhiteSpace(language) ? "pt-BR" : language.Trim();
        Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();
        UpdatedAt = DateTime.UtcNow;
    }

    private static string SanitizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 13 && digits.StartsWith("55"))
            digits = digits.Substring(2);

        if (digits.Length == 11)
            return $"({digits.Substring(0, 2)}) {digits.Substring(2, 5)}-{digits.Substring(7, 4)}";

        return phone.Trim();
    }

    private static DateTime? NormalizeUtcDate(DateTime? date)
    {
        if (!date.HasValue) return null;
        var dt = date.Value;
        if (dt.Kind == DateTimeKind.Utc) return dt;
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    private static string NormalizeIntelligenceMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "B" : mode.Trim().ToUpperInvariant();
        if (normalized is "B" or "C") return normalized;
        throw new ArgumentException("Modo de inteligência inválido. Use B ou C.");
    }

    private static int NormalizeCarryOverDay(int value)
    {
        if (value is >= 1 and <= 31) return value;
        throw new ArgumentException("CarryOverDay inválido. Use um valor entre 1 e 31.");
    }
}
