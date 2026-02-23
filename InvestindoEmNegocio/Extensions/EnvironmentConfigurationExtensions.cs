namespace InvestindoEmNegocio.Extensions;

public static class EnvironmentConfigurationExtensions
{
    public static void LoadEnvironmentVariablesFromConfiguration(this WebApplicationBuilder builder)
    {
        var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
        if (File.Exists(envPath))
            DotNetEnv.Env.Load(envPath);

        foreach (var (envKey, configKey) in new (string EnvKey, string ConfigKey)[]
                 {
                     ("ConnectionStrings__Default", "ConnectionStrings:Default"),
                     ("Jwt__Issuer", "Jwt:Issuer"),
                     ("Jwt__Audience", "Jwt:Audience"),
                     ("Jwt__SecretKey", "Jwt:SecretKey"),
                     ("Jwt__ExpiresMinutes", "Jwt:ExpiresMinutes"),
                     ("OTEL_SERVICE_NAME", "OTEL_SERVICE_NAME"),
                     ("OTEL_EXPORTER_OTLP_ENDPOINT", "OTEL_EXPORTER_OTLP_ENDPOINT"),
                     ("OTEL_EXPORTER_OTLP_PROTOCOL", "OTEL_EXPORTER_OTLP_PROTOCOL"),
                     ("OTEL_TRACES_EXPORTER", "OTEL_TRACES_EXPORTER"),
                     ("OTEL_METRICS_EXPORTER", "OTEL_METRICS_EXPORTER"),
                     ("OTEL_LOGS_EXPORTER", "OTEL_LOGS_EXPORTER"),
                     ("DataPortability__Enabled", "DataPortability:Enabled"),
                     ("DataPortability__MaxImportSizeMb", "DataPortability:MaxImportSizeMb"),
                     ("DataPortability__ExportCacheSeconds", "DataPortability:ExportCacheSeconds"),
                     ("B3Api__Enabled", "B3Api:Enabled"),
                     ("B3Api__BaseUrl", "B3Api:BaseUrl"),
                     ("B3Api__ClientId", "B3Api:ClientId"),
                     ("B3Api__ClientSecret", "B3Api:ClientSecret"),
                     ("Cors__AllowedOrigins", "Cors:AllowedOrigins"),
                     ("MarketData__Provider", "MarketData:Provider"),
                     ("MarketData__BrapiToken", "MarketData:BrapiToken"),
                     ("PasswordReset__FrontendResetUrl", "PasswordReset:FrontendResetUrl"),
                     ("PasswordReset__TokenExpiryMinutes", "PasswordReset:TokenExpiryMinutes"),
                     ("Smtp__Host", "Smtp:Host"),
                     ("Smtp__Port", "Smtp:Port"),
                     ("Smtp__EnableSsl", "Smtp:EnableSsl"),
                     ("Smtp__Username", "Smtp:Username"),
                     ("Smtp__Password", "Smtp:Password"),
                     ("Smtp__FromEmail", "Smtp:FromEmail"),
                     ("Smtp__FromName", "Smtp:FromName"),
                     ("Robots__Enabled", "Robots:Enabled"),
                     ("Robots__RunOnStartup", "Robots:RunOnStartup"),
                     ("Robots__DailyRunTimeUtc", "Robots:DailyRunTimeUtc")
                 })
        {
            EnsureEnvFromConfig(builder.Configuration, envKey, configKey);
        }
    }

    private static void EnsureEnvFromConfig(IConfiguration config, string envKey, string configKey)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envKey)))
            return;

        var value = config[configKey];
        if (!string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(envKey, value);
    }
}
