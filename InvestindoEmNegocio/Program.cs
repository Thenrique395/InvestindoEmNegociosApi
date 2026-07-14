using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicy = "AllowFrontend";
var isDevelopment = builder.Environment.IsDevelopment();

builder.LoadEnvironmentVariablesFromConfiguration();
var applySchemaOnStartup = builder.Configuration.GetValue<bool?>("Database:ApplySchemaOnStartup") ?? true;
var bootstrapOnly = builder.Configuration.GetValue<bool>("Database:BootstrapOnly");
var otelSettings = builder.AddAppObservability();

builder.Services
    .AddApiSurface(isDevelopment)
    .AddAppOptions(builder.Configuration)
    .AddAppCors(CorsPolicy, builder.Configuration)
    .AddAppRateLimiting()
    .AddPersistence(builder.Configuration)
    .AddApplicationDependencies()
    .AddValidation()
    .AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization(AppAuthorizationPolicies.Configure);

var app = builder.Build();

app.LogOtelConfiguration(otelSettings);
app.MapApiDocs();
app.UseAppPipeline(CorsPolicy);

app.MapControllers();
app.MapHealthEndpoints();

if (applySchemaOnStartup)
{
    await app.ApplyDatabaseSchemaAsync();
}

await app.SeedReferenceDataAsync();

if (bootstrapOnly)
{
    app.Logger.LogInformation("BootstrapOnly habilitado. Encerrando aplicação após preparar o banco.");
    return;
}

app.Run();
