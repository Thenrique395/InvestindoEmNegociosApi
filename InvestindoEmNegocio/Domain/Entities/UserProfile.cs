namespace InvestindoEmNegocio.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
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
        string language = "pt-BR",
        string currency = "BRL",
        int carryOverDay = 1,
        string financialGoal = "",
        string intelligenceMode = "B")
    {
        UserId = userId;
        UpdateProfileData(language, currency, carryOverDay, financialGoal, intelligenceMode);
    }

    public void UpdateProfileData(
        string language = "pt-BR",
        string currency = "BRL",
        int carryOverDay = 1,
        string financialGoal = "",
        string intelligenceMode = "B")
    {
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
