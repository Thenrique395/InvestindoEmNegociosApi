namespace InvestindoEmNegocio.Domain.Entities;

public sealed class RobotSettings
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool Enabled { get; private set; } = true;
    public string DailyRunTimeUtc { get; private set; } = "08:00";
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private RobotSettings() { }

    public RobotSettings(bool enabled, string dailyRunTimeUtc)
    {
        Update(enabled, dailyRunTimeUtc);
    }

    public void Update(bool enabled, string dailyRunTimeUtc)
    {
        var normalized = NormalizeTime(dailyRunTimeUtc);
        Enabled = enabled;
        DailyRunTimeUtc = normalized;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeTime(string value)
    {
        var candidate = (value ?? string.Empty).Trim();
        if (!TimeOnly.TryParse(candidate, out var parsed))
        {
            parsed = new TimeOnly(8, 0);
        }

        return $"{parsed.Hour:00}:{parsed.Minute:00}";
    }
}
