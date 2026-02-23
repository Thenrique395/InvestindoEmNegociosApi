namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class RobotsOptions
{
    public const string SectionName = "Robots";

    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public string DailyRunTimeUtc { get; set; } = "08:00";
}
