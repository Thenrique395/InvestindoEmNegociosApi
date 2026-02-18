namespace InvestindoEmNegocio.Application.Services;

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public string Provider { get; set; } = "free";
    public int QuoteCacheMinutes { get; set; } = 5;
    public int ProfileCacheMinutes { get; set; } = 60;
    public int HistoryCacheMinutes { get; set; } = 30;
}
