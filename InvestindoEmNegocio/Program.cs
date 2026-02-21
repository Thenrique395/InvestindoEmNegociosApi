using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicy = "AllowFrontend";
var isDevelopment = builder.Environment.IsDevelopment();

builder.LoadEnvironmentVariablesFromConfiguration();
var otelSettings = builder.AddAppObservability();

builder.Services
    .AddApiSurface(isDevelopment)
    .AddAppOptions(builder.Configuration)
    .AddAppCors(builder.Configuration, CorsPolicy)
    .AddAppRateLimiting()
    .AddPersistence(builder.Configuration)
    .AddApplicationDependencies()
    .AddValidation()
    .AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

app.LogOtelConfiguration(otelSettings);
app.MapApiDocs();
app.UseAppPipeline(CorsPolicy);

app.MapControllers();
app.MapHealthEndpoints();

await app.ApplyDatabaseSchemaAsync();

app.Run();
