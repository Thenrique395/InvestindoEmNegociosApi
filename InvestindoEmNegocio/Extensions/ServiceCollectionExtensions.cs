using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using InvestindoEmNegocio.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace InvestindoEmNegocio.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSurface(this IServiceCollection services, bool isDevelopment)
    {
        services.AddEndpointsApiExplorer();
        services.AddMemoryCache();
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/problem+json"
            });
        });

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Any();
                var allowsAnonymous = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAllowAnonymous>()
                    .Any();

                if (!requiresAuth || allowsAnonymous)
                    return Task.CompletedTask;

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = []
                });

                return Task.CompletedTask;
            });
        });

        services.AddHttpContextAccessor();
        services.AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Dados inválidos.",
                    Detail = "Revise os campos informados.",
                    Instance = context.HttpContext.Request.Path
                };

                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                if (isDevelopment && context.Exception is not null)
                    context.ProblemDetails.Extensions["exception"] = context.Exception.GetType().Name;
            };
        });

        return services;
    }

    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<DataPortabilityOptions>(configuration.GetSection("DataPortability"));
        services.Configure<B3ApiOptions>(configuration.GetSection("B3Api"));
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.Configure<SmtpEmailOptions>(configuration.GetSection(SmtpEmailOptions.SectionName));
        services.Configure<RobotsOptions>(configuration.GetSection(RobotsOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.Configure<AuthCookieOptions>(configuration.GetSection(AuthCookieOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddAppCors(this IServiceCollection services, string corsPolicy)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(corsPolicy, policy =>
            {
                // CORS aberto — restringir por origem ao migrar para produção com HTTPS.
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("admin-robots", limiterOptions =>
            {
                limiterOptions.PermitLimit = 20;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 2;
            });

            options.AddFixedWindowLimiter("stripe-webhook", limiterOptions =>
            {
                limiterOptions.PermitLimit = 200;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("mercadopago-webhook", limiterOptions =>
            {
                limiterOptions.PermitLimit = 200;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });

            // Particionado por usuário (não global como os limiters acima) — checkout é uma
            // ação autenticada por usuário; um limiter global deixaria um único usuário
            // esgotar a cota de todo mundo.
            options.AddPolicy("billing-checkout", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Particionado por usuário, mesmo motivo do billing-checkout — cada chamada ao
            // assistente financeiro agora tem custo real (API da Anthropic), então um limiter
            // global deixaria um único usuário esgotar a cota de todo mundo.
            options.AddPolicy("financial-assistant-chat", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Default' não configurada.");

        services.AddDbContext<InvestDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(InvestDbContext).Assembly.GetName().Name);
            }));
        services.AddScoped<IInvestDbContext>(sp => sp.GetRequiredService<InvestDbContext>());

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddNpgSql(connectionString, name: "db", timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBillingCheckoutRepository, BillingCheckoutRepository>();
        services.AddScoped<IBillingWebhookEventRepository, BillingWebhookEventRepository>();
        services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
        services.AddScoped<IUserFeatureOverrideRepository, UserFeatureOverrideRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ISpaceRepository, SpaceRepository>();
        services.AddScoped<IAccountTransactionRepository, AccountTransactionRepository>();
        services.AddScoped<ICardBrandRepository, CardBrandRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IGoalContributionRepository, GoalContributionRepository>();
        services.AddScoped<IMoneyPlanRepository, MoneyPlanRepository>();
        services.AddScoped<IMoneyInstallmentRepository, MoneyInstallmentRepository>();
        services.AddScoped<IMoneyPaymentRepository, MoneyPaymentRepository>();
        services.AddScoped<IInvestmentGoalRepository, InvestmentGoalRepository>();
        services.AddScoped<IInvestmentAllocationTargetRepository, InvestmentAllocationTargetRepository>();
        services.AddScoped<IInvestmentPositionRepository, InvestmentPositionRepository>();
        services.AddScoped<ILoanContractRepository, LoanContractRepository>();
        services.AddScoped<ILoanInstallmentRepository, LoanInstallmentRepository>();
        services.AddScoped<IMonthlyFinancialSnapshotRepository, MonthlyFinancialSnapshotRepository>();
        services.AddScoped<IUserOnboardingRepository, UserOnboardingRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<INotificationSettingsRepository, NotificationSettingsRepository>();
        services.AddScoped<IRobotSettingsRepository, RobotSettingsRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentSpaceAccessor, CurrentSpaceAccessor>();
        services.AddScoped<IAuthCookieService, AuthCookieService>();
        services.AddScoped<IAuthRegistrationService, AuthRegistrationService>();
        services.AddScoped<IAuthAvailabilityService, AuthAvailabilityService>();
        services.AddScoped<IAuthAccessService, AuthAccessService>();
        services.AddScoped<IAuthPasswordService, AuthPasswordService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthRegistrationApplicationService, AuthRegistrationApplicationService>();
        services.AddScoped<IAuthAccessApplicationService, AuthAccessApplicationService>();
        services.AddScoped<IAuthPasswordApplicationService, AuthPasswordApplicationService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IImportIdentityEngine, ImportIdentityEngine>();
        services.AddScoped<IBankStatementImportEngine, BankStatementImportEngine>();
        services.AddScoped<IMonthlyBudgetRepository, MonthlyBudgetRepository>();
        services.AddScoped<IRobotTask, ReminderRobotTask>();
        services.AddScoped<IRobotTask, SubscriptionExpirationRobotTask>();
        services.AddScoped<IRobotTask, MonthlySnapshotRobotTask>();
        services.AddScoped<IRobotTask, AiFinancialHealthRobotTask>();
        services.AddScoped<IRobotRunner, RobotRunner>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IProfileQueryService, ProfileQueryService>();
        services.AddScoped<IProfileCommandService, ProfileCommandService>();
        services.AddScoped<IInvestmentGoalQueryService, InvestmentPlanningService>();
        services.AddScoped<IInvestmentGoalCommandService, InvestmentPlanningService>();
        services.AddScoped<IInvestmentAllocationQueryService, InvestmentPlanningService>();
        services.AddScoped<IInvestmentAllocationCommandService, InvestmentPlanningService>();
        services.AddScoped<IInvestmentPositionQueryService, InvestmentPositionService>();
        services.AddScoped<IInvestmentPositionCommandService, InvestmentPositionService>();
        services.AddScoped<IInvestmentMarketEnrichmentService, InvestmentMarketEnrichmentService>();
        services.AddScoped<IInvestmentsService, InvestmentsService>();
        services.AddScoped<IInvestmentPortfolioCommandService, InvestmentPortfolioCommandService>();
        services.AddScoped<IInvestmentMarketIntegrationService, InvestmentMarketIntegrationService>();
        services.AddScoped<IInvestmentsApplicationService, InvestmentsApplicationService>();
        services.AddScoped<IOnboardingQueryService, OnboardingService>();
        services.AddScoped<IOnboardingCommandService, OnboardingService>();
        services.AddScoped<ICardsService, CardsService>();
        services.AddScoped<ICashflowProjectionEngine, CashflowProjectionEngine>();
        services.AddScoped<IRiskBotService, RiskBotService>();
        services.AddScoped<IInsightEngineService, InsightEngineService>();
        services.AddScoped<IRecommendationEngineService, RecommendationEngineService>();
        services.AddScoped<IAccountQueryService, AccountQueryService>();
        services.AddScoped<IAccountCommandService, AccountCommandService>();
        services.AddScoped<IAccountTransactionQueryService, AccountTransactionQueryService>();
        services.AddScoped<IAccountTransferService, AccountTransferService>();
        services.AddScoped<IAccountsService, AccountsService>();
        services.AddScoped<IAccountAnalyticsService, AccountAnalyticsService>();
        services.AddScoped<ILoansService, LoansService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IScenariosService, ScenariosService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IMonthlyFinancialSnapshotService, MonthlyFinancialSnapshotService>();
        services.AddScoped<IFinancialAssistantService, FinancialAssistantService>();
        services.AddScoped<IAiFinancialHealthService, AiFinancialHealthService>();
        services.AddScoped<IUserAccountBootstrapService, UserAccountBootstrapService>();
        services.AddScoped<ISpaceBootstrapService, SpaceBootstrapService>();
        services.AddScoped<ISpaceService, SpaceService>();
        services.AddScoped<IOfxImportService, OfxImportService>();
        services.AddScoped<ICsvImportService, CsvImportService>();
        services.AddScoped<ICategoriesService, CategoriesService>();
        services.AddScoped<ICategorizationService, CategorizationService>();
        services.AddScoped<IRecurrenceDetectorService, RecurrenceDetectorService>();
        services.AddScoped<IGoalsService, GoalsService>();
        services.AddScoped<IGoalContributionsService, GoalContributionsService>();
        services.AddScoped<IInstallmentsService, InstallmentsService>();
        services.AddScoped<ILookupPaymentMethodService, LookupsService>();
        services.AddScoped<ILookupCardBrandService, LookupsService>();
        services.AddScoped<ILookupInstitutionService, LookupsService>();
        services.AddScoped<IPlansService, PlansService>();
        services.AddScoped<IIncomeSummaryService, IncomeSummaryService>();
        services.AddScoped<IPreferenceSettingsService, PreferenceSettingsService>();
        services.AddScoped<IUserPrivacyCenterService, UserPrivacyCenterService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IBillingNotificationService, BillingNotificationService>();
        services.AddScoped<IStripeBillingGateway, StripeBillingGateway>();
        // IPaymentProvider aponta para Stripe por padrão — assinaturas Mercado Pago serão
        // resolvidas por subscription.Provider quando o roteamento de checkout for implementado.
        services.AddScoped<IPaymentProvider, StripeBillingGateway>();
        services.AddHttpClient<MercadoPagoBillingGateway>((sp, client) =>
        {
            var mpOptions = sp.GetRequiredService<IOptions<MercadoPagoOptions>>().Value;
            client.BaseAddress = new Uri(mpOptions.ApiBaseUrl);
        });
        services.AddScoped<IMercadoPagoBillingGateway>(sp => sp.GetRequiredService<MercadoPagoBillingGateway>());
        services.AddHttpClient<AnthropicClient>((sp, client) =>
        {
            var anthropicOptions = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            client.BaseAddress = new Uri(anthropicOptions.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IAnthropicClient>(sp => sp.GetRequiredService<AnthropicClient>());
        services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
        services.AddScoped<IBillingCheckoutCommandService, BillingCheckoutCommandService>();
        services.AddScoped<IBillingCheckoutQueryService, BillingCheckoutQueryService>();
        services.AddScoped<IBillingPortalService, BillingPortalService>();
        services.AddScoped<IBillingSubscriptionSyncService, BillingSubscriptionSyncService>();
        services.AddScoped<IStripeBillingWebhookProcessor, StripeBillingWebhookProcessor>();
        services.AddScoped<IStripeBillingWebhookService, StripeBillingWebhookService>();
        services.AddScoped<IMercadoPagoBillingWebhookService, MercadoPagoBillingWebhookService>();
        services.AddScoped<ISubscriptionCatalogService, SubscriptionCatalogService>();
        services.AddScoped<ISubscriptionManagementService, SubscriptionManagementService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<INotificationGenerationService, NotificationGenerationService>();
        services.AddScoped<INotificationCommandService, NotificationCommandService>();
        services.AddScoped<IAdminUsersService, AdminUsersService>();
        services.AddScoped<IAdminPaymentMethodsService, AdminPaymentMethodsService>();
        services.AddScoped<IAdminCardBrandsService, AdminCardBrandsService>();
        services.AddScoped<IAdminInstitutionsService, AdminInstitutionsService>();
        services.AddScoped<IAdminNotificationSettingsService, AdminNotificationSettingsService>();
        services.AddScoped<IAdminRobotSettingsService, AdminRobotSettingsService>();
        services.AddScoped<IAdminEmailDiagnosticsService, AdminEmailDiagnosticsService>();
        services.AddScoped<IAdminParametersService, AdminParametersService>();
        services.AddScoped<IAdminCategoriesService, AdminCategoriesService>();
        services.AddScoped<IAdminRobotMonitorService, AdminRobotsService>();
        services.AddScoped<IAdminRobotExecutionService, AdminRobotsService>();
        services.AddScoped<IAdminRuntimeInfoService, AdminRuntimeInfoService>();
        services.AddSingleton<InvoiceParserFactory>();
        services.AddScoped<IInvoiceImportService, InvoiceImportService>();
        services.AddScoped<IB3ImportService, B3ImportService>();
        services.AddScoped<IMarketDataService, MarketDataService>();
        services.AddScoped<IMarketDataProvider, FreeMarketDataProvider>();
        services.AddScoped<IMarketDataProvider, B3MarketDataProvider>();
        services.AddScoped<IAvatarStorageService, AvatarStorageService>();
        services.AddScoped<IReceiptStorageService, ReceiptStorageService>();

        services.AddHttpClient<IInvestmentBenchmarksService, InvestmentBenchmarksService>(client =>
        {
            client.BaseAddress = new Uri("https://api.bcb.gov.br/");
            client.Timeout = TimeSpan.FromSeconds(20);
        })
        .AddExternalApiResilience(
            timeoutPerTry: TimeSpan.FromSeconds(8),
            retryCount: 3,
            baseDelay: TimeSpan.FromMilliseconds(200),
            breakerHandledEventsAllowedBeforeBreaking: 5,
            breakerDuration: TimeSpan.FromSeconds(30));

        services.AddHttpClient("MarketBrapi", client =>
        {
            client.BaseAddress = new Uri("https://brapi.dev/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("InvestindoEmNegocio/1.0 (+market-data)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .AddExternalApiResilience(
            timeoutPerTry: TimeSpan.FromSeconds(8),
            retryCount: 3,
            baseDelay: TimeSpan.FromMilliseconds(250),
            breakerHandledEventsAllowedBeforeBreaking: 5,
            breakerDuration: TimeSpan.FromSeconds(30));

        services.AddHttpClient<IB3Connector, B3ApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<B3ApiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                client.BaseAddress = new Uri(opts.BaseUrl);

            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds <= 0 ? 30 : opts.TimeoutSeconds);
        })
        .AddExternalApiResilience(
            timeoutPerTry: TimeSpan.FromSeconds(12),
            retryCount: 3,
            baseDelay: TimeSpan.FromMilliseconds(300),
            breakerHandledEventsAllowedBeforeBreaking: 4,
            breakerDuration: TimeSpan.FromSeconds(45));
        services.AddScoped<IB3SyncService, B3SyncService>();
        services.AddScoped<IDataPortabilityExportService, DataPortabilityExportService>();
        services.AddScoped<IDataPortabilityImportService, DataPortabilityImportService>();
        services.AddScoped<IDataPortabilityService, DataPortabilityService>();
        services.AddScoped<IDataPortabilityGuardService, DataPortabilityGuardService>();
        services.AddScoped<IDataPortabilityApplicationService, DataPortabilityApplicationService>();
        services.AddScoped<IClaimsTransformation, UserFeatureClaimsTransformation>();
        services.AddHostedService<RobotsHostedService>();

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<InvestindoEmNegocio.Application.Validation.RegisterUserRequestValidator>();
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                         ?? throw new InvalidOperationException("Configuração JWT não encontrada.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.StartsWith("CHANGE_ME"))
            throw new InvalidOperationException("JWT SecretKey não configurada ou usa valor placeholder. Configure via variável de ambiente.");

        services.AddAuthentication(options =>
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
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(AuthCookieService.AccessTokenCookie, out var accessToken)
                            && !string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    // Revogação imediata de sessão: sem isso, desativar um usuário ou trocar a
                    // senha não tem efeito sobre tokens já emitidos até eles expirarem (15 min).
                    // Deliberadamente sem cache aqui — é o trade-off "correção > micro-otimização"
                    // para a revogação ser de fato imediata, não "imediata em até N segundos".
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var tokenVersionClaim = context.Principal?.FindFirst(JwtTokenGenerator.TokenVersionClaim)?.Value;

                        if (!Guid.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("Token inválido.");
                            return;
                        }

                        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var user = await userRepository.GetByIdAsync(userId, context.HttpContext.RequestAborted);

                        if (user is null || !user.IsActive || user.TokenVersion.ToString() != tokenVersionClaim)
                        {
                            context.Fail("Sessão revogada.");
                        }
                    }
                };
            });

        return services;
    }
}
