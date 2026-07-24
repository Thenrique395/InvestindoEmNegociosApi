using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;

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

// DataProtection: persiste as chaves fora do container para que dados protegidos
// (antiforgery/XSRF, etc.) sobrevivam a restart/deploy. Sem isso as chaves ficam
// em /root/.aspnet/DataProtection-Keys (efêmero) e são perdidas a cada deploy.
// O caminho vem de config (DataProtection:KeysPath); vazio em dev local => efêmero.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("InvestindoEmNegocio");
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

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
