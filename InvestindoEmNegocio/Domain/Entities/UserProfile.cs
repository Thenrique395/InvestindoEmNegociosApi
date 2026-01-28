using System.Linq;

namespace InvestindoEmNegocio.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Document { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime? BirthDate { get; private set; }
    public string AvatarUrl { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string Language { get; private set; } = "pt-BR";
    public string Currency { get; private set; } = "BRL";
    public bool NotifyUpcomingEnabled { get; private set; } = true;
    public bool NotifyOverdueEnabled { get; private set; } = true;
    public bool NotifyEmailEnabled { get; private set; } = false;
    public bool NotifyInAppEnabled { get; private set; } = true;
    public int NotifyDaysBeforeDue { get; private set; } = 3;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    // EF
    private UserProfile() { }

    public UserProfile(Guid userId, string fullName, string document, string phone, DateTime? birthDate, string avatarUrl = "", string city = "", string state = "", string country = "", string language = "pt-BR", string currency = "BRL")
    {
        UserId = userId;
        SetData(fullName, document, phone, birthDate, avatarUrl, city, state, country, language, currency);
    }

    public void SetData(string fullName, string document, string phone, DateTime? birthDate, string avatarUrl = "", string city = "", string state = "", string country = "", string language = "pt-BR", string currency = "BRL")
    {
        FullName = fullName.Trim();
        Document = SanitizeDocument(document);
        Phone = SanitizePhone(phone);
        BirthDate = NormalizeDate(birthDate);
        AvatarUrl = avatarUrl?.Trim() ?? string.Empty;
        City = city?.Trim() ?? string.Empty;
        State = state?.Trim() ?? string.Empty;
        Country = country?.Trim() ?? string.Empty;
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

    private static string SanitizeDocument(string document)
    {
        var digits = new string(document.Where(char.IsDigit).ToArray());
        return digits;
    }

    private static string SanitizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length != 13)
        {
            return phone.Trim();
        }

        var country = digits.Substring(0, 2);
        var area = digits.Substring(2, 2);
        var number = digits.Substring(4);
        return $"+{country} {area} {number}";
    }

    private static DateTime? NormalizeDate(DateTime? date)
    {
        if (!date.HasValue) return null;
        var dt = date.Value;
        if (dt.Kind == DateTimeKind.Utc) return dt;
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
