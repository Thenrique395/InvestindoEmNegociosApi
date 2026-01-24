using System.Text;
using System.Text.Json.Serialization;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Logging;
using InvestindoEmNegocio.Infrastructure.Swagger;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using Serilog.Sinks.OpenTelemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicy = "AllowFrontend";

var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
}

static void EnsureEnvFromConfig(IConfiguration config, string envKey, string configKey)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envKey)))
    {
        return;
    }

    var value = config[configKey];
    if (!string.IsNullOrWhiteSpace(value))
    {
        Environment.SetEnvironmentVariable(envKey, value);
    }
}

EnsureEnvFromConfig(builder.Configuration, "ConnectionStrings__Default", "ConnectionStrings:Default");
EnsureEnvFromConfig(builder.Configuration, "Jwt__Issuer", "Jwt:Issuer");
EnsureEnvFromConfig(builder.Configuration, "Jwt__Audience", "Jwt:Audience");
EnsureEnvFromConfig(builder.Configuration, "Jwt__SecretKey", "Jwt:SecretKey");
EnsureEnvFromConfig(builder.Configuration, "Jwt__ExpiresMinutes", "Jwt:ExpiresMinutes");
EnsureEnvFromConfig(builder.Configuration, "OTEL_SERVICE_NAME", "OTEL_SERVICE_NAME");
EnsureEnvFromConfig(builder.Configuration, "OTEL_EXPORTER_OTLP_ENDPOINT", "OTEL_EXPORTER_OTLP_ENDPOINT");
EnsureEnvFromConfig(builder.Configuration, "OTEL_EXPORTER_OTLP_PROTOCOL", "OTEL_EXPORTER_OTLP_PROTOCOL");
EnsureEnvFromConfig(builder.Configuration, "OTEL_TRACES_EXPORTER", "OTEL_TRACES_EXPORTER");
EnsureEnvFromConfig(builder.Configuration, "OTEL_METRICS_EXPORTER", "OTEL_METRICS_EXPORTER");
EnsureEnvFromConfig(builder.Configuration, "OTEL_LOGS_EXPORTER", "OTEL_LOGS_EXPORTER");

var otelServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "InvestindoEmNegocio";
var otlpEndpointValue = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var otlpProtocolValue = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
if (string.IsNullOrWhiteSpace(otlpEndpointValue))
{
    Console.WriteLine("OTEL_EXPORTER_OTLP_ENDPOINT não definido. OTEL não enviará dados.");
}

static OtlpExportProtocol ResolveOtlpProtocol(string? protocolValue)
{
    if (string.IsNullOrWhiteSpace(protocolValue))
    {
        return OtlpExportProtocol.Grpc;
    }

    return protocolValue.Trim().ToLowerInvariant() switch
    {
        "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
        "grpc" => OtlpExportProtocol.Grpc,
        _ => OtlpExportProtocol.Grpc
    };
}

static Uri? ResolveOtlpEndpoint(string? endpointValue)
{
    if (string.IsNullOrWhiteSpace(endpointValue))
    {
        return null;
    }

    return Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
        ? endpoint
        : null;
}

var otlpEndpoint = ResolveOtlpEndpoint(otlpEndpointValue);
var otlpProtocol = ResolveOtlpProtocol(otlpProtocolValue);
var otelResource = ResourceBuilder.CreateDefault()
    .AddService(otelServiceName);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "InvestindoEmNegocio")
        .WriteTo.Console(new RenderedCompactJsonFormatter());

    if (otlpEndpoint is not null)
    {
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint.ToString();
            options.Protocol = otlpProtocol == OtlpExportProtocol.Grpc
                ? OtlpProtocol.Grpc
                : OtlpProtocol.HttpProtobuf;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = otelServiceName
            };
        });
    }
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<FileUploadOperationFilter>();
    options.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            if (otlpEndpoint is not null)
            {
                options.Endpoint = otlpEndpoint;
            }

            options.Protocol = otlpProtocol;
        }))
    .WithMetrics(metricProviderBuilder => metricProviderBuilder
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options =>
        {
            if (otlpEndpoint is not null)
            {
                options.Endpoint = otlpEndpoint;
            }

            options.Protocol = otlpProtocol;
        }));
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Aceita enums como string (case-insensitive) e bloqueia inteiros
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    });
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (builder.Environment.IsDevelopment() && context.Exception is not null)
        {
            context.ProblemDetails.Extensions["exception"] = context.Exception.GetType().Name;
        }
    };
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "http://192.168.1.87:4200",
                "http://localhost:4000",
                "http://127.0.0.1:4000",
                "http://192.168.1.87:4000",
                "https://localhost:4200",
                "https://127.0.0.1:4200",
                "https://192.168.1.87:4200",
                "https://localhost:4000",
                "https://127.0.0.1:4000",
                "https://192.168.1.87:4000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Default' não configurada.");
}

builder.Services.AddDbContext<InvestDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(InvestDbContext).Assembly.GetName().Name);
    }));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddNpgSql(connectionString, name: "db", timeout: TimeSpan.FromSeconds(5));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<ICardRepository, CardRepository>();
builder.Services.AddScoped<ICardBrandRepository, CardBrandRepository>();
builder.Services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IGoalContributionRepository, GoalContributionRepository>();
builder.Services.AddScoped<IMoneyPlanRepository, MoneyPlanRepository>();
builder.Services.AddScoped<IMoneyInstallmentRepository, MoneyInstallmentRepository>();
builder.Services.AddScoped<IMoneyPaymentRepository, MoneyPaymentRepository>();
builder.Services.AddScoped<IInvestmentGoalRepository, InvestmentGoalRepository>();
builder.Services.AddScoped<IInvestmentPositionRepository, InvestmentPositionRepository>();
builder.Services.AddScoped<IUserOnboardingRepository, UserOnboardingRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IInstitutionRepository, InstitutionRepository>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IInvestmentsService, InvestmentsService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<ICardsService, CardsService>();
builder.Services.AddScoped<ICategoriesService, CategoriesService>();
builder.Services.AddScoped<IGoalsService, GoalsService>();
builder.Services.AddScoped<IGoalContributionsService, GoalContributionsService>();
builder.Services.AddScoped<IInstallmentsService, InstallmentsService>();
builder.Services.AddScoped<ILookupsService, LookupsService>();
builder.Services.AddScoped<IPlansService, PlansService>();
builder.Services.AddScoped<IPreferencesService, PreferencesService>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<InvestindoEmNegocio.Application.Validation.RegisterUserRequestValidator>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Configuração JWT não encontrada.");

if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    throw new InvalidOperationException("JWT SecretKey não configurada.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

Log.Information("OTEL config loaded. Endpoint: {OtelEndpoint}, Protocol: {OtelProtocol}, ServiceName: {OtelServiceName}",
    otlpEndpoint?.ToString() ?? "<not-set>", otlpProtocol, otelServiceName);

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno do servidor.",
            Detail = app.Environment.IsDevelopment()
                ? exceptionHandler?.Error.Message
                : null,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        if (app.Environment.IsDevelopment() && exceptionHandler?.Error is not null)
        {
            problemDetails.Extensions["exception"] = exceptionHandler.Error.GetType().Name;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User?.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            diagnosticContext.Set("UserId", userId);
        }
    };
});

app.UseRouting();

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true
}).AllowAnonymous();
app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate = check => check.Name == "db"
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name == "db"
}).AllowAnonymous();

await app.ApplyMigrationsAsync();

app.Run();
