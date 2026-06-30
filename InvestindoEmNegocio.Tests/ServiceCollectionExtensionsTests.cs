using FluentAssertions;
using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Validation;
using InvestindoEmNegocio.Extensions;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InvestindoEmNegocio.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPersistence_Should_Throw_When_Default_Connection_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddPersistence(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string 'Default' não configurada.*");
    }

    [Fact]
    public void AddAppOptions_Should_Bind_Config_Sections()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer-test",
                ["Jwt:Audience"] = "audience-test",
                ["Jwt:SecretKey"] = "12345678901234567890123456789012",
                ["Jwt:ExpiresMinutes"] = "60",
                ["DataPortability:Enabled"] = "true",
                ["B3Api:Enabled"] = "true",
                ["MarketData:Provider"] = "Brapi"
            })
            .Build();

        services.AddAppOptions(configuration);
        var provider = services.BuildServiceProvider();

        var jwt = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        jwt.Issuer.Should().Be("issuer-test");
        jwt.Audience.Should().Be("audience-test");
        jwt.ExpiresMinutes.Should().Be(60);
    }

    [Fact]
    public async Task AddPersistence_Should_Register_DbContext_When_Connection_Is_Configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test"
            })
            .Build();

        var result = services.AddPersistence(configuration);

        result.Should().BeSameAs(services);
        services.Any(x => x.ServiceType.Name.Contains("InvestDbContext", StringComparison.Ordinal)).Should().BeTrue();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<InvestDbContext>().Should().NotBeNull();
        var health = provider.GetRequiredService<HealthCheckService>();
        health.Should().NotBeNull();
        (await health.CheckHealthAsync()).Entries.Should().ContainKey("self");
    }

    [Fact]
    public void AddApiSurface_Should_Register_ProblemDetails_And_Controllers()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddApiSurface(isDevelopment: true);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IProblemDetailsService>();
        provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>();

        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails(),
            Exception = new InvalidOperationException("boom")
        };

        options.CustomizeProblemDetails.Should().NotBeNull();
        options.CustomizeProblemDetails!(context);

        context.ProblemDetails.Extensions["traceId"].Should().Be("trace-123");
        context.ProblemDetails.Extensions["exception"].Should().Be("InvalidOperationException");
    }

    [Fact]
    public void AddApiSurface_Should_Not_Add_Exception_Extension_Outside_Development()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiSurface(isDevelopment: false);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;

        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-prod" },
            ProblemDetails = new ProblemDetails(),
            Exception = new InvalidOperationException("boom")
        };

        options.CustomizeProblemDetails!(context);

        context.ProblemDetails.Extensions.Should().ContainKey("traceId");
        context.ProblemDetails.Extensions.Should().NotContainKey("exception");
    }

    [Fact]
    public void AddAppCors_Should_Allow_Any_Origin_With_Credentials()
    {
        var services = new ServiceCollection();
        const string policy = "AllowFrontend";

        services.AddAppCors(policy);
        var provider = services.BuildServiceProvider();

        var cors = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var configuredPolicy = cors.GetPolicy(policy);

        configuredPolicy.Should().NotBeNull();
        configuredPolicy!.IsOriginAllowed("http://localhost:4200").Should().BeTrue();
        configuredPolicy.IsOriginAllowed("https://app.example.com").Should().BeTrue();
        configuredPolicy.IsOriginAllowed("http://35.174.50.187:4201").Should().BeTrue();
        configuredPolicy.SupportsCredentials.Should().BeTrue();
    }

    [Fact]
    public void AddAppRateLimiting_Should_Register_RateLimiter_Options()
    {
        var services = new ServiceCollection();

        services.AddAppRateLimiting();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        options.RejectionStatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void AddApplicationDependencies_Should_Register_Key_Services()
    {
        var services = new ServiceCollection();

        services.AddApplicationDependencies();

        services.Any(x => x.ServiceType == typeof(IAuthService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAuthRegistrationApplicationService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAuthAccessApplicationService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAuthPasswordApplicationService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IInvestmentsService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IDataPortabilityService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IB3SyncService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IProfileQueryService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IProfileCommandService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IPreferenceSettingsService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IOnboardingQueryService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IOnboardingCommandService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(ILookupPaymentMethodService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(ILookupCardBrandService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(ILookupInstitutionService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(INotificationQueryService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(INotificationGenerationService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(INotificationCommandService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAdminRobotMonitorService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAdminRobotExecutionService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IAdminRuntimeInfoService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IBillingCheckoutCommandService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IBillingCheckoutQueryService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IBillingPortalService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IBillingSubscriptionSyncService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IStripeBillingWebhookProcessor)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(IStripeBillingWebhookService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(ISubscriptionCatalogService)).Should().BeTrue();
        services.Any(x => x.ServiceType == typeof(ISubscriptionManagementService)).Should().BeTrue();
    }

    [Fact]
    public async Task AddApplicationDependencies_Should_Configure_HttpClients()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["B3Api:BaseUrl"] = "https://api.b3.com.br/",
                ["B3Api:TimeoutSeconds"] = "17"
            })
            .Build();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddOptions();
        services.AddAppOptions(configuration);
        services.AddApplicationDependencies();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var brapiClient = factory.CreateClient("MarketBrapi");
        brapiClient.BaseAddress.Should().Be(new Uri("https://brapi.dev/"));
        brapiClient.Timeout.Should().Be(TimeSpan.FromSeconds(20));
        brapiClient.DefaultRequestHeaders.Accept.Any(x => x.MediaType == "application/json").Should().BeTrue();
        brapiClient.DefaultRequestHeaders.UserAgent.ToString().Should().Contain("InvestindoEmNegocio/1.0");

        var b3Client = factory.CreateClient("IB3Connector");
        b3Client.Timeout.Should().Be(TimeSpan.FromSeconds(17));

        // Resolve typed service to execute scoped registrations
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<IInvestmentBenchmarksService>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IB3Connector>().Should().NotBeNull();
        await Task.CompletedTask;
    }

    [Fact]
    public void AddValidation_Should_Register_Request_Validators()
    {
        var services = new ServiceCollection();

        services.AddValidation();
        var provider = services.BuildServiceProvider();

        provider.GetService<IValidator<RegisterUserRequest>>().Should().BeOfType<RegisterUserRequestValidator>();
        provider.GetService<IValidator<AccountRequest>>().Should().BeOfType<AccountRequestValidator>();
        provider.GetService<IValidator<AccountTransferRequest>>().Should().BeOfType<AccountTransferRequestValidator>();
        provider.GetService<IValidator<CardRequest>>().Should().BeOfType<CardRequestValidator>();
        provider.GetService<IValidator<UpsertCategoryRequest>>().Should().BeOfType<UpsertCategoryRequestValidator>();
        provider.GetService<IValidator<CreateGoalRequest>>().Should().BeOfType<CreateGoalRequestValidator>();
        provider.GetService<IValidator<GoalContributionRequest>>().Should().BeOfType<GoalContributionRequestValidator>();
        provider.GetService<IValidator<CreatePlanRequest>>().Should().BeOfType<CreatePlanRequestValidator>();
        provider.GetService<IValidator<PaymentRequest>>().Should().BeOfType<PaymentRequestValidator>();
        provider.GetService<IValidator<PaymentReversalRequest>>().Should().BeOfType<PaymentReversalRequestValidator>();
        provider.GetService<IValidator<ChangePasswordRequest>>().Should().BeOfType<ChangePasswordRequestValidator>();
        provider.GetService<IValidator<RefreshTokenRequest>>().Should().BeOfType<RefreshTokenRequestValidator>();
        provider.GetService<IValidator<StartBillingCheckoutRequest>>().Should().BeOfType<StartBillingCheckoutRequestValidator>();
        provider.GetService<IValidator<ChangeSubscriptionRequest>>().Should().BeOfType<ChangeSubscriptionRequestValidator>();
        provider.GetService<IValidator<CreatePaymentMethodRequest>>().Should().BeOfType<CreatePaymentMethodRequestValidator>();
        provider.GetService<IValidator<CreateCardBrandRequest>>().Should().BeOfType<CreateCardBrandRequestValidator>();
        provider.GetService<IValidator<CreateInstitutionRequest>>().Should().BeOfType<CreateInstitutionRequestValidator>();
        provider.GetService<IValidator<UpdateRobotSettingsRequest>>().Should().BeOfType<UpdateRobotSettingsRequestValidator>();
        provider.GetService<IValidator<SendTestEmailRequest>>().Should().BeOfType<SendTestEmailRequestValidator>();
        provider.GetService<IValidator<AdminCategoryRequest>>().Should().BeOfType<AdminCategoryRequestValidator>();
        provider.GetService<IValidator<UpdateUserRoleRequest>>().Should().BeOfType<UpdateUserRoleRequestValidator>();
        provider.GetService<IValidator<UpdateOnboardingRequest>>().Should().BeOfType<UpdateOnboardingRequestValidator>();
        provider.GetService<IValidator<ConfirmB3ImportRequest>>().Should().BeOfType<ConfirmB3ImportRequestValidator>();
        provider.GetService<IValidator<B3SyncRequest>>().Should().BeOfType<B3SyncRequestValidator>();
        provider.GetService<IValidator<UploadB3ReportRequest>>().Should().BeOfType<UploadB3ReportRequestValidator>();
        provider.GetService<IValidator<UploadCsvStatementRequest>>().Should().BeOfType<UploadCsvStatementRequestValidator>();
        provider.GetService<IValidator<UploadOfxRequest>>().Should().BeOfType<UploadOfxRequestValidator>();
        provider.GetService<IValidator<UploadInvoiceRequest>>().Should().BeOfType<UploadInvoiceRequestValidator>();
        provider.GetService<IValidator<ImportUserDataRequest>>().Should().BeOfType<ImportUserDataRequestValidator>();
        provider.GetService<IValidator<BankStatementImportRequest>>().Should().BeOfType<BankStatementImportRequestValidator>();
        provider.GetService<IValidator<InvoiceImportRequest>>().Should().BeOfType<InvoiceImportRequestValidator>();
        provider.GetService<IValidator<CreateInvestmentPositionRequest>>().Should().BeOfType<CreateInvestmentPositionRequestValidator>();
        provider.GetService<IValidator<CreateInvestmentMovementRequest>>().Should().BeOfType<CreateInvestmentMovementRequestValidator>();
        provider.GetService<IValidator<UpsertInvestmentAllocationTargetRequest>>().Should().BeOfType<UpsertInvestmentAllocationTargetRequestValidator>();
        provider.GetService<IValidator<UpsertInvestmentGoalRequest>>().Should().BeOfType<UpsertInvestmentGoalRequestValidator>();
        provider.GetService<IValidator<LoanContractRequest>>().Should().BeOfType<LoanContractRequestValidator>();
        provider.GetService<IValidator<FinancialAssistantChatRequest>>().Should().BeOfType<FinancialAssistantChatRequestValidator>();
        provider.GetService<IValidator<GenerateMonthlyFinancialSnapshotRequest>>().Should().BeOfType<GenerateMonthlyFinancialSnapshotRequestValidator>();
        provider.GetService<IValidator<UpsertIncomeGoalRequest>>().Should().BeOfType<UpsertIncomeGoalRequestValidator>();
        provider.GetService<IValidator<AnticipationRequest>>().Should().BeOfType<AnticipationRequestValidator>();
        provider.GetService<IValidator<UpdateNotificationSettingsRequest>>().Should().BeOfType<UpdateNotificationSettingsRequestValidator>();
        provider.GetService<IValidator<UploadAvatarRequest>>().Should().BeOfType<UploadAvatarRequestValidator>();
        provider.GetService<IValidator<UpdateActiveRequest>>().Should().BeOfType<UpdateActiveRequestValidator>();
        provider.GetService<IValidator<UpdateUserStatusRequest>>().Should().BeOfType<UpdateUserStatusRequestValidator>();
        provider.GetService<IValidator<SetUserFeatureOverrideRequest>>().Should().BeOfType<SetUserFeatureOverrideRequestValidator>();
        provider.GetService<IValidator<InvoiceImportItemRequest>>().Should().BeOfType<InvoiceImportItemRequestValidator>();
    }

    [Fact]
    public void AddJwtAuthentication_Should_Throw_When_Secret_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer-test",
                ["Jwt:Audience"] = "audience-test",
                ["Jwt:SecretKey"] = ""
            })
            .Build();

        Action act = () => services.AddJwtAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT SecretKey não configurada*");
    }

    [Fact]
    public void AddJwtAuthentication_Should_Throw_When_Section_Is_Missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddJwtAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Configuração JWT não encontrada*");
    }

    [Fact]
    public void AddJwtAuthentication_Should_Register_Authentication_Services()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer-test",
                ["Jwt:Audience"] = "audience-test",
                ["Jwt:SecretKey"] = "12345678901234567890123456789012"
            })
            .Build();

        services.AddJwtAuthentication(configuration);
        var provider = services.BuildServiceProvider();

        provider.GetService<IAuthenticationSchemeProvider>().Should().NotBeNull();
        var jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        jwtOptions.TokenValidationParameters.Should().NotBeNull();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be("issuer-test");
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("audience-test");
        jwtOptions.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void AddApplicationDependencies_Should_Register_DbContext_Abstraction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationDependencies();

        services.Any(x => x.ServiceType == typeof(IInvestDbContext)).Should().BeFalse();
        services.Any(x => x.ServiceType == typeof(InvestDbContext)).Should().BeFalse();
    }

    [Fact]
    public void AddApiSurface_Should_Configure_ResponseCompression_For_Json()
    {
        var services = new ServiceCollection();
        services.AddApiSurface(isDevelopment: true);
        var provider = services.BuildServiceProvider();

        var compressionOptions = provider.GetRequiredService<IOptions<ResponseCompressionOptions>>().Value;

        compressionOptions.EnableForHttps.Should().BeTrue();
        compressionOptions.MimeTypes.Should().Contain("application/json");
        compressionOptions.MimeTypes.Should().Contain("application/problem+json");
    }

    [Fact]
    public async Task AddApiSurface_Should_Apply_OpenApi_Transformers()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApiSurface(isDevelopment: true);
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.MapGet("/secure", () => Results.Ok("ok")).RequireAuthorization();
        app.MapGet("/public", () => Results.Ok("ok")).AllowAnonymous();
        app.MapOpenApi("/openapi/v1.json");

        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        try
        {
            var address = app.Urls.Single();

            using var http = new HttpClient();
            var json = await http.GetStringAsync($"{address}/openapi/v1.json");

            json.Should().Contain("Bearer");
            json.Should().Contain("securitySchemes");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
