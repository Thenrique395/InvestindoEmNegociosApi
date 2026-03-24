namespace InvestindoEmNegocio.Extensions;

public static class EnvironmentConfigurationExtensions
{
    public static void LoadEnvironmentVariablesFromConfiguration(this WebApplicationBuilder builder)
    {
        var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
        if (File.Exists(envPath))
            DotNetEnv.Env.Load(envPath);

        // Support existing legacy/simple env names from .env and CI (ex.: SMTP_HOST, DB_CONN).
        PromoteEnvAlias("DB_CONN", "ConnectionStrings__Default");
        PromoteEnvAlias("JWT_ISSUER", "Jwt__Issuer");
        PromoteEnvAlias("JWT_AUDIENCE", "Jwt__Audience");
        PromoteEnvAlias("JWT_SECRET_KEY", "Jwt__SecretKey");
        PromoteEnvAlias("JWT_EXPIRES_MINUTES", "Jwt__ExpiresMinutes");
        PromoteEnvAlias("BRAPI_TOKEN", "MarketData__BrapiToken");
        PromoteEnvAlias("PASSWORD_RESET_FRONTEND_URL", "PasswordReset__FrontendResetUrl");
        PromoteEnvAlias("PASSWORD_RESET_TOKEN_EXPIRY_MINUTES", "PasswordReset__TokenExpiryMinutes");
        PromoteEnvAlias("SMTP_HOST", "Smtp__Host");
        PromoteEnvAlias("SMTP_PORT", "Smtp__Port");
        PromoteEnvAlias("SMTP_ENABLE_SSL", "Smtp__EnableSsl");
        PromoteEnvAlias("SMTP_USER", "Smtp__Username");
        PromoteEnvAlias("SMTP_PASS", "Smtp__Password");
        PromoteEnvAlias("SMTP_FROM_EMAIL", "Smtp__FromEmail");
        PromoteEnvAlias("SMTP_FROM_NAME", "Smtp__FromName");
        PromoteEnvAlias("STRIPE_SECRET_KEY", "Stripe__SecretKey");
        PromoteEnvAlias("STRIPE_WEBHOOK_SECRET", "Stripe__WebhookSecret");
        PromoteEnvAlias("STRIPE_PUBLISHABLE_KEY", "Stripe__PublishableKey");
        PromoteEnvAlias("STRIPE_FRONTEND_BASE_URL", "Stripe__FrontendBaseUrl");
        PromoteEnvAlias("STRIPE_PAYMENT_METHOD_TYPES", "Stripe__PaymentMethodTypes");
        PromoteEnvAlias("ROBOTS_ENABLED", "Robots__Enabled");
        PromoteEnvAlias("ROBOTS_RUN_ON_STARTUP", "Robots__RunOnStartup");
        PromoteEnvAlias("ROBOTS_DAILY_RUN_TIME_UTC", "Robots__DailyRunTimeUtc");

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
                     ("Stripe__SecretKey", "Stripe:SecretKey"),
                     ("Stripe__WebhookSecret", "Stripe:WebhookSecret"),
                     ("Stripe__PublishableKey", "Stripe:PublishableKey"),
                     ("Stripe__FrontendBaseUrl", "Stripe:FrontendBaseUrl"),
                     ("Stripe__PortalReturnPath", "Stripe:PortalReturnPath"),
                     ("Stripe__SuccessPath", "Stripe:SuccessPath"),
                     ("Stripe__CancelPath", "Stripe:CancelPath"),
                     ("Stripe__FailedPath", "Stripe:FailedPath"),
                     ("Stripe__PaymentMethodTypes", "Stripe:PaymentMethodTypes"),
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

    private static void PromoteEnvAlias(string sourceEnvKey, string targetEnvKey)
    {
        var source = Environment.GetEnvironmentVariable(sourceEnvKey);
        if (string.IsNullOrWhiteSpace(source))
            return;

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(targetEnvKey)))
            return;

        Environment.SetEnvironmentVariable(targetEnvKey, source);
    }
}
