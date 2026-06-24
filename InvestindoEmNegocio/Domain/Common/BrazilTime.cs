namespace InvestindoEmNegocio.Domain.Common;

public static class BrazilTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime NowLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    public static DateOnly TodayLocal => DateOnly.FromDateTime(NowLocal);

    public static DateTime TodayStartUtc => TimeZoneInfo.ConvertTimeToUtc(NowLocal.Date, TimeZone);

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback se a imagem não tiver tzdata instalado: Brasília não observa horário de verão desde 2019.
            return TimeZoneInfo.CreateCustomTimeZone(
                "America/Sao_Paulo_Fixed",
                TimeSpan.FromHours(-3),
                "Horário de Brasília (fixo)",
                "Horário de Brasília (fixo)");
        }
    }
}
