using System.Security.Claims;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class MoreControllersSmokeTests
{
    [Fact]
    public async Task CardsController_Should_Cover_Main_Actions()
    {
        var cards = new Mock<ICardsService>();
        var cardResponse = new CardResponse(Guid.NewGuid(), 1, "User", "Cartao", "1234", null, 1000m, 10, 20, DateTime.UtcNow, DateTime.UtcNow);
        cards.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CardResponse>());
        cards.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<CardRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(cardResponse);
        cards.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CardRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(cardResponse);
        cards.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cards.Setup(x => x.GetTotalDebtAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(123m);
        cards.Setup(x => x.ListStatementCyclesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CardStatementCycleResponse>());
        var audit = new Mock<IAuditService>();
        var c = new CardsController(cards.Object, audit.Object);
        var cardDebtController = new CardDebtController(cards.Object);
        var cardStatementsController = new CardStatementsController(cards.Object);
        SetAuth(c);
        SetAuth(cardDebtController);
        SetAuth(cardStatementsController);

        var req = new CardRequest(1, "User", "Cartao", "1234", null, 1000m, 10, 20);
        (await c.List(new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Create(req, CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await c.Update(Guid.NewGuid(), req, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await cardDebtController.GetTotal(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await cardStatementsController.List(Guid.NewGuid(), 2026, 3, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AccountsController_Should_Cover_Transfer_And_Transactions()
    {
        var accounts = new Mock<IAccountsService>();
        var accountAnalytics = new Mock<IAccountAnalyticsService>();
        var accountId = Guid.NewGuid();
        accounts.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AccountResponse(accountId, "Conta", AccountType.Checking, 0, 100, true, DateTime.UtcNow, DateTime.UtcNow)]);
        accounts.Setup(x => x.GetBalanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountBalanceResponse(accountId, 0, 100, 100));
        accountAnalytics.Setup(x => x.GetRealAvailableBalanceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealAvailableBalanceResponse("month", new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), 1000m, 200m, 2, 300m, 1, 800m, 1100m, 50m, 1, 75m));
        accountAnalytics.Setup(x => x.GetProjectionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashflowProjectionResponse("month", new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 31), 1000m, 850m, 500m, new DateOnly(2026, 3, 12), null, [
                new CashflowProjectionPointResponse(new DateOnly(2026, 3, 9), 1000m, 0m, 0, 200m, 1, 800m)
            ]));
        accountAnalytics.Setup(x => x.GetRiskAssessmentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskBotAssessmentResponse("month", new DateOnly(2026, 3, 9), 62, "warning", "warning", null, 80m, 110m, 850m, ["pending_income"], ["Base: 100"], [
                new RiskBotRecommendationResponse("pending-income", "info", "Próxima receita pendente.", "Abrir receitas", "/incomes", new Dictionary<string, string> { ["focus"] = "pending" }, 100m, new DateOnly(2026, 3, 10))
            ]));
        accountAnalytics.Setup(x => x.GetInsightsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InsightEngineResponse("month", new DateOnly(2026, 3, 9),
                new InsightEngineItemResponse("preventive", "preventive-upcoming-window", "warning", "Janela preventiva", "Há pressão de vencimentos próxima.", "Acompanhar vencimentos.", 62, null, 80m, 110m, 850m, ["Score: 62/100"], ["Reserve caixa"], ["pending_income"], ["Base: 100"], [
                    new RiskBotRecommendationResponse("pending-income", "info", "Próxima receita pendente.", "Abrir receitas", "/incomes", new Dictionary<string, string> { ["focus"] = "pending" }, 100m, new DateOnly(2026, 3, 10))
                ]),
                [
                    new InsightEngineItemResponse("preventive", "preventive-upcoming-window", "warning", "Janela preventiva", "Há pressão de vencimentos próxima.", "Acompanhar vencimentos.", 62, null, 80m, 110m, 850m, ["Score: 62/100"], ["Reserve caixa"], ["pending_income"], ["Base: 100"], [
                    new RiskBotRecommendationResponse("pending-income", "info", "Próxima receita pendente.", "Abrir receitas", "/incomes", new Dictionary<string, string> { ["focus"] = "pending" }, 100m, new DateOnly(2026, 3, 10))
                    ])
                ]));
        accountAnalytics.Setup(x => x.GetRecommendationsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationEngineResponse("month", new DateOnly(2026, 3, 9), 50, [
                new RecommendationItemResponse("due-soon-expenses", 83, "warn", "risk-bot", "Ação recomendada", "Há despesas vencendo.", "Ver próximas despesas", "/expenses", new Dictionary<string, string> { ["focus"] = "upcoming" }, ["due_soon_expenses"], 100m, new DateOnly(2026, 3, 10))
            ]));
        accounts.Setup(x => x.ListTransactionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AccountTransactionResponse(
                Guid.NewGuid(),
                accountId,
                DateTime.UtcNow,
                AccountTransactionType.Transfer,
                AccountTransactionKind.Debit,
                50,
                "Transfer",
                "AccountTransfer",
                "Transfer",
                "Transferência",
                Guid.NewGuid(),
                DateTime.UtcNow)]);
        accounts.Setup(x => x.TransferAsync(It.IsAny<Guid>(), It.IsAny<AccountTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountTransferResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50, DateTime.UtcNow, "Transfer"));

        var accountsController = new AccountsController(accounts.Object, accounts.Object);
        var summariesController = new AccountSummariesController(accountAnalytics.Object);
        var insightsController = new AccountInsightsController(accountAnalytics.Object);
        var transactionsController = new AccountTransactionsController(accounts.Object);
        var transfersController = new AccountTransfersController(accounts.Object);
        SetAuth(accountsController, UserRole.Intermediate);
        SetAuth(summariesController, UserRole.Intermediate);
        SetAuth(insightsController, UserRole.Intermediate);
        SetAuth(transactionsController, UserRole.Intermediate);
        SetAuth(transfersController, UserRole.Intermediate);

        (await accountsController.List(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await accountsController.Balance(accountId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await summariesController.RealBalance("month", new DateOnly(2026, 3, 9), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await summariesController.Projection("month", new DateOnly(2026, 3, 9), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await insightsController.Risk("month", new DateOnly(2026, 3, 9), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await insightsController.Insights("month", new DateOnly(2026, 3, 9), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await insightsController.Recommendations("month", new DateOnly(2026, 3, 9), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await transactionsController.List(accountId, null, null, null, null, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await transfersController.Create(new AccountTransferRequest(Guid.NewGuid(), Guid.NewGuid(), 50), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CategoriesController_Should_Cover_Main_Actions_And_Error_Mapping()
    {
        var service = new Mock<ICategoriesService>();
        var categoryResponse = new CategoryResponse(Guid.NewGuid(), "Cat", MoneyType.Expense, false, true);
        service.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<MoneyType?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CategoryResponse>());
        service.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<UpsertCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(categoryResponse);
        service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpsertCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(categoryResponse);
        service.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(CategoryDeletionOutcome.Deleted);
        var c = new CategoriesController(service.Object, Mock.Of<IAuditService>());
        SetAuth(c);

        (await c.List(null, false, new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Create(new UpsertCategoryRequest("Cat", MoneyType.Expense), CancellationToken.None)).Should().BeOfType<CreatedResult>();
        (await c.Update(Guid.NewGuid(), new UpsertCategoryRequest("Cat", MoneyType.Expense), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        service.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<UpsertCategoryRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("bad"));
        Func<Task> bad = async () => await c.Create(new UpsertCategoryRequest("Cat", null), CancellationToken.None);
        await bad.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task InstallmentsController_Should_Cover_Main_Actions_And_Branches()
    {
        var service = new Mock<IInstallmentsService>();
        service.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<InstallmentStatus?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<MoneyType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InstallmentResponse>());
        service.Setup(x => x.PayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        service.Setup(x => x.AnticipateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AnticipationRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        service.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var c = new InstallmentsController(service.Object, Mock.Of<IAuditService>());
        var paymentsController = new InstallmentPaymentsController(service.Object);
        var anticipationsController = new InstallmentAnticipationsController(service.Object);
        SetAuth(c);
        SetAuth(paymentsController);
        SetAuth(anticipationsController);

        (await c.List(null, null, null, null, new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await paymentsController.Pay(Guid.NewGuid(), new PaymentRequest(DateTime.UtcNow, 10m), CancellationToken.None)).Should().BeOfType<OkResult>();
        (await anticipationsController.Create(Guid.NewGuid(), new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None)).Should().BeOfType<OkResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        service.Setup(x => x.AnticipateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AnticipationRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("x"));
        Func<Task> anticipateBad = async () => await anticipationsController.Create(Guid.NewGuid(), new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);
        await anticipateBad.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Lightweight_Controllers_Should_Return_Ok_Or_NoContent()
    {
        var notificationQuery = new Mock<INotificationQueryService>();
        var notificationGeneration = new Mock<INotificationGenerationService>();
        var notificationCommand = new Mock<INotificationCommandService>();
        notificationQuery.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<NotificationDto>());
        notificationGeneration.Setup(x => x.GenerateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var notificationsController = new NotificationsController(notificationQuery.Object, notificationGeneration.Object, notificationCommand.Object);
        SetAuth(notificationsController);
        (await notificationsController.List(false, 10, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationsController.Generate(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationsController.MarkRead(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        var lookupPaymentMethods = new Mock<ILookupPaymentMethodService>();
        var lookupCardBrands = new Mock<ILookupCardBrandService>();
        var lookupInstitutions = new Mock<ILookupInstitutionService>();
        lookupPaymentMethods.Setup(x => x.GetPaymentMethodsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PaymentMethod>());
        lookupCardBrands.Setup(x => x.GetCardBrandsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CardBrand>());
        lookupInstitutions.Setup(x => x.GetInstitutionsAsync(It.IsAny<InstitutionType?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Institution>());
        var lookupsController = new LookupsController(lookupPaymentMethods.Object, lookupCardBrands.Object, lookupInstitutions.Object);
        (await lookupsController.GetPaymentMethods(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await lookupsController.GetCardBrands(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await lookupsController.GetInstitutions("Bank", CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var prefs = new Mock<IPreferenceSettingsService>();
        prefs.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PreferencesDto)!);
        prefs.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdatePreferencesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PreferencesDto)!);
        var prefsController = new PreferencesController(prefs.Object);
        SetAuth(prefsController);
        (await prefsController.Get(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await prefsController.Update(new UpdatePreferencesRequest("BRL", [], null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var onboardingQuery = new Mock<IOnboardingQueryService>();
        var onboardingCommand = new Mock<IOnboardingCommandService>();
        onboardingQuery.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(OnboardingStatusDto)!);
        onboardingCommand.Setup(x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<UpdateOnboardingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(OnboardingStatusDto)!);
        var onboardingController = new OnboardingController(onboardingQuery.Object, onboardingCommand.Object);
        SetAuth(onboardingController);
        (await onboardingController.Get(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await onboardingController.Update(new UpdateOnboardingRequest(1, true), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Billing_And_Subscriptions_Controllers_Should_Cover_Query_And_Command_Flows()
    {
        var billingCheckoutCommand = new Mock<IBillingCheckoutCommandService>();
        var billingCheckoutQuery = new Mock<IBillingCheckoutQueryService>();
        var billingPortal = new Mock<IBillingPortalService>();
        var billingWebhook = new Mock<IStripeBillingWebhookService>();
        var subscriptionCatalog = new Mock<ISubscriptionCatalogService>();
        var subscriptionManagement = new Mock<ISubscriptionManagementService>();

        billingCheckoutCommand
            .Setup(x => x.StartCheckoutAsync(It.IsAny<Guid>(), It.IsAny<StartBillingCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartBillingCheckoutResponse(Guid.NewGuid(), "stripe", "Pending", "https://checkout.test", "advanced", "Monthly", 59.90m, "BRL", DateTime.UtcNow.AddMinutes(30)));
        billingCheckoutQuery
            .Setup(x => x.GetCheckoutStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingCheckoutStatusResponse(Guid.NewGuid(), "stripe", "Pending", "advanced", "Monthly", 59.90m, "BRL", "cs_test", "sub_test", "unpaid", false, false, true, false, false, DateTime.UtcNow.AddMinutes(30), null, null, null, null, null));
        billingCheckoutQuery
            .Setup(x => x.GetCheckoutStatusByProviderSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingCheckoutStatusResponse(Guid.NewGuid(), "stripe", "Paid", "advanced", "Monthly", 59.90m, "BRL", "cs_test", "sub_test", "paid", false, false, true, true, true, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), null, null, null));
        billingPortal
            .Setup(x => x.CreatePortalSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingPortalSessionResponse("https://portal.test"));
        subscriptionCatalog
            .Setup(x => x.GetCatalogAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionCatalogResponse(
                new CurrentSubscriptionResponse("basic", "Basic", "Basic", "Active", "Monthly", 0m, "BRL", false, DateTime.UtcNow, null, null),
                [],
                []));
        subscriptionManagement
            .Setup(x => x.ChangeAsync(It.IsAny<Guid>(), It.IsAny<ChangeSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionChangeResponse(
                new CurrentSubscriptionResponse("basic", "Basic", "Basic", "Active", "Monthly", 0m, "BRL", false, DateTime.UtcNow, null, null),
                new AuthResponse(Guid.NewGuid(), "U", "u@test.com", "Basic", "token", "refresh", DateTime.UtcNow.AddHours(1)),
                []));
        subscriptionManagement
            .Setup(x => x.CancelAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionChangeResponse(
                new CurrentSubscriptionResponse("advanced", "Avançado", "Basic", "Cancelled", "Monthly", 59.90m, "BRL", false, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(10), DateTime.UtcNow),
                new AuthResponse(Guid.NewGuid(), "U", "u@test.com", "Basic", "token", "refresh", DateTime.UtcNow.AddHours(1)),
                []));

        var billingCheckoutsController = new BillingCheckoutsController(billingCheckoutCommand.Object, billingCheckoutQuery.Object);
        var billingPortalController = new BillingPortalController(billingPortal.Object);
        var subscriptionsController = new SubscriptionsController(subscriptionCatalog.Object, subscriptionManagement.Object, new AuthCookieService(Options.Create(new AuthCookieOptions())));
        var webhookController = new StripeWebhooksController(billingWebhook.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        SetAuth(billingCheckoutsController);
        SetAuth(billingPortalController);
        SetAuth(subscriptionsController);
        webhookController.ControllerContext.HttpContext.Request.Body = new MemoryStream("""{"id":"evt_1"}"""u8.ToArray());
        webhookController.ControllerContext.HttpContext.Request.Headers["Stripe-Signature"] = "sig_test";

        (await billingCheckoutsController.Start(new StartBillingCheckoutRequest("advanced", "Monthly"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await billingCheckoutsController.GetStatus(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await billingCheckoutsController.GetStatusBySession("cs_test", CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await billingPortalController.Create(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await subscriptionsController.GetCatalog(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await subscriptionsController.Change(new ChangeSubscriptionRequest("basic", "Monthly"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await subscriptionsController.Cancel(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await webhookController.Receive(CancellationToken.None)).Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Auth_And_Privacy_Controllers_Should_Cover_Main_Flows()
    {
        var authAccess = new Mock<IAuthAccessApplicationService>();
        var authRegistration = new Mock<IAuthRegistrationApplicationService>();
        var authPassword = new Mock<IAuthPasswordApplicationService>();
        var privacy = new Mock<IUserPrivacyCenterService>();

        authAccess.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponse(Guid.NewGuid(), "U", "u@test.com", "Basic", "token", "refresh", DateTime.UtcNow.AddHours(1)));
        authAccess.Setup(x => x.RefreshAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponse(Guid.NewGuid(), "U", "u@test.com", "Basic", "token", "refresh", DateTime.UtcNow.AddHours(1)));
        authRegistration.Setup(x => x.RegisterAsync(It.IsAny<RegisterUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponse(Guid.NewGuid(), "U", "u@test.com", "Basic", "token", "refresh", DateTime.UtcNow.AddHours(1)));
        privacy.Setup(x => x.GetPrivacySummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrivacySummaryDto(1, 0, 2, true, true, [], [], "policy"));
        privacy.Setup(x => x.GetSecuritySummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecuritySummaryDto(1, 0, false, null, null, [], []));
        privacy.Setup(x => x.RevokeOwnSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevokeSessionsResponse(2, DateTime.UtcNow));

        var authAvailability = new Mock<IAuthAvailabilityService>();
        authAvailability.Setup(x => x.CheckAsync(It.IsAny<CheckAvailabilityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckAvailabilityResponse(false, false));
        var authCookieService = new AuthCookieService(Options.Create(new AuthCookieOptions()));
        var authController = new AuthController(authAccess.Object, authAvailability.Object, authCookieService);
        var authRegistrationController = new AuthRegistrationController(authRegistration.Object, authCookieService);
        var authPasswordsController = new AuthPasswordsController(authPassword.Object);
        var summariesController = new PreferenceSummariesController(privacy.Object);
        var sessionsController = new PreferenceSessionsController(privacy.Object);
        var accountController = new PreferenceAccountController(privacy.Object);
        SetAuth(authController);
        SetAuth(authRegistrationController);
        SetAuth(authPasswordsController);
        SetAuth(summariesController);
        SetAuth(sessionsController);
        SetAuth(accountController);
        authController.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{AuthCookieService.RefreshTokenCookie}=refresh";

        (await authController.Login(new LoginRequest("u@test.com", "123"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await authController.Refresh(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await authController.Logout(CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await authRegistrationController.Register(new RegisterUserRequest("U", "u@test.com", "123456", "52998224725"), CancellationToken.None)).Result.Should().BeOfType<CreatedAtActionResult>();
        (await authPasswordsController.ChangePassword(new ChangePasswordRequest("old", "new"), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await authPasswordsController.ForgotPassword(new ForgotPasswordRequest("u@test.com"), CancellationToken.None)).Should().BeOfType<AcceptedResult>();
        (await authPasswordsController.ResetPassword(new ResetPasswordRequest("token", "new"), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await summariesController.GetPrivacy(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await summariesController.GetSecurity(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await sessionsController.RevokeOwn(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await accountController.DeleteOwn(new DeleteOwnAccountRequest("123", "EXCLUIR"), CancellationToken.None)).Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DataPortability_Profile_And_GoalContributions_Should_Cover_Main_Flows()
    {
        var dp = new Mock<IDataPortabilityApplicationService>();
        dp.Setup(x => x.ExportAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(("export.json", "{}"u8.ToArray()));
        dp.Setup(x => x.ImportAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ImportUserDataResult(1));
        var dpController = new DataPortabilityController(dp.Object);
        SetAuth(dpController);
        (await dpController.Export(CancellationToken.None)).Should().BeOfType<FileContentResult>();
        var importFile = BuildFormFile("application/json");
        (await dpController.Import(new ImportUserDataRequest { File = importFile, ReplaceExisting = false }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        Func<Task> missingImport = async () => await dpController.Import(new ImportUserDataRequest(), CancellationToken.None);
        await missingImport.Should().ThrowAsync<AppProblemException>();

        var profileQueryService = new Mock<IProfileQueryService>();
        var profileCommandService = new Mock<IProfileCommandService>();
        profileQueryService.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserProfileDto?)null);
        profileCommandService.Setup(x => x.UpsertAsync(It.IsAny<Guid>(), It.IsAny<UpsertUserProfileRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserProfileDto)!);
        profileCommandService.Setup(x => x.UpdateAvatarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserProfileDto)!);
        var avatar = new Mock<IAvatarStorageService>();
        avatar.Setup(x => x.SaveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("https://x");
        var profileController = new ProfileController(profileQueryService.Object, profileCommandService.Object, avatar.Object);
        SetAuth(profileController);
        profileController.ControllerContext.HttpContext.Request.Scheme = "https";
        profileController.ControllerContext.HttpContext.Request.Host = new HostString("example.com");
        (await profileController.Get(CancellationToken.None)).Result.Should().BeOfType<NoContentResult>();
        (await profileController.Upsert(new UpsertUserProfileRequest("A", "2", null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "pt-BR"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await profileController.UploadAvatar(new UploadAvatarRequest { Avatar = BuildFormFile("image/png") }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        var contrib = new Mock<IGoalContributionsService>();
        var contribution = new GoalContributionResponse(Guid.NewGuid(), 10m, DateOnly.FromDateTime(DateTime.UtcNow), null, DateTime.UtcNow);
        contrib.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<GoalContributionResponse>());
        contrib.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<GoalContributionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(contribution);
        var contribController = new GoalContributionsController(contrib.Object);
        SetAuth(contribController);
        (await contribController.List(Guid.NewGuid(), new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await contribController.Create(Guid.NewGuid(), new GoalContributionRequest(10m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None)).Should().BeOfType<CreatedResult>();

        contrib.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<GoalContributionRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("bad"));
        Func<Task> createBad = async () => await contribController.Create(Guid.NewGuid(), new GoalContributionRequest(10m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);
        await createBad.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Admin_Controllers_Should_Return_Ok_For_Main_Actions()
    {
        var adminUsers = new Mock<IAdminUsersService>();
        adminUsers.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<UserSummaryResponse>());
        adminUsers.Setup(x => x.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserSummaryResponse)!);
        adminUsers.Setup(x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserSummaryResponse)!);
        var adminUsersController = new AdminUsersController(adminUsers.Object);
        SetAuth(adminUsersController);
        (await adminUsersController.List(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminUsersController.UpdateRole(Guid.NewGuid(), new UpdateUserRoleRequest("Admin"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminUsersController.UpdateStatus(Guid.NewGuid(), new UpdateUserStatusRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminUsersController.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        var adminCategories = new Mock<IAdminCategoriesService>();
        adminCategories.Setup(x => x.ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AdminCategoryResponse>());
        adminCategories.Setup(x => x.CreateAsync(It.IsAny<AdminCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(AdminCategoryResponse)!);
        adminCategories.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<AdminCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(AdminCategoryResponse)!);
        adminCategories.Setup(x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(AdminCategoryResponse)!);
        var adminCategoriesController = new AdminCategoriesController(adminCategories.Object);
        (await adminCategoriesController.List(true, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminCategoriesController.Create(new AdminCategoryRequest("Cat", "Expense"), CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await adminCategoriesController.Update(Guid.NewGuid(), new AdminCategoryRequest("Cat", "Expense"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminCategoriesController.UpdateStatus(Guid.NewGuid(), new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var adminParameters = new Mock<IAdminParametersService>();
        adminParameters.Setup(x => x.ListPaymentMethodsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PaymentMethodAdminResponse>());
        adminParameters.Setup(x => x.UpdatePaymentMethodStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PaymentMethodAdminResponse)!);
        adminParameters.Setup(x => x.CreatePaymentMethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PaymentMethodAdminResponse)!);
        adminParameters.Setup(x => x.ListCardBrandsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CardBrandAdminResponse>());
        adminParameters.Setup(x => x.UpdateCardBrandStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(CardBrandAdminResponse)!);
        adminParameters.Setup(x => x.CreateCardBrandAsync(It.IsAny<CreateCardBrandRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(CardBrandAdminResponse)!);
        adminParameters.Setup(x => x.ListInstitutionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<InstitutionAdminResponse>());
        adminParameters.Setup(x => x.CreateInstitutionAsync(It.IsAny<CreateInstitutionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(InstitutionAdminResponse)!);
        adminParameters.Setup(x => x.UpdateInstitutionStatusAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(InstitutionAdminResponse)!);
        adminParameters.Setup(x => x.GetNotificationSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(default(NotificationSettingsDto)!);
        adminParameters.Setup(x => x.UpdateNotificationSettingsAsync(It.IsAny<UpdateNotificationSettingsRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(NotificationSettingsDto)!);
        var adminRobotMonitor = new Mock<IAdminRobotMonitorService>();
        var adminRobotExecution = new Mock<IAdminRobotExecutionService>();
        var adminRuntime = new Mock<IAdminRuntimeInfoService>();
        adminRobotMonitor.Setup(x => x.MonitorAsync(It.IsAny<RobotMonitorQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RobotMonitorResponseDto(new RobotMonitorSummaryDto(0, 0, 0, 0, 0, 0, 0, 0), [], []));
        adminRobotExecution.Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RobotRunResultDto("ReminderRobot", DateTime.UtcNow, DateTime.UtcNow, 0, "corr", "host", null, true, 0, new RobotExecutionMetricsDto(0, 0, 0, 0, null), false, null, null));
        adminRobotExecution.Setup(x => x.RunAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RobotRunResultDto>());
        adminRuntime.Setup(x => x.Get())
            .Returns(new ScalabilityRuntimeDto("phase-1-runtime-hardened", [], [], []));
        var paymentMethodsController = new AdminPaymentMethodsController(adminParameters.Object);
        var cardBrandsController = new AdminCardBrandsController(adminParameters.Object);
        var institutionsController = new AdminInstitutionsController(adminParameters.Object);
        var notificationSettingsController = new AdminNotificationSettingsController(adminParameters.Object);
        var adminRobotsController = new AdminRobotsController(adminRobotMonitor.Object, adminRobotExecution.Object);
        var adminRuntimeController = new AdminRuntimeController(adminRuntime.Object);
        (await paymentMethodsController.List(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await paymentMethodsController.UpdateStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await paymentMethodsController.Create(new CreatePaymentMethodRequest("Pix"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await cardBrandsController.List(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await cardBrandsController.UpdateStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await cardBrandsController.Create(new CreateCardBrandRequest("Visa", "VISA"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await institutionsController.List(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await institutionsController.Create(new CreateInstitutionRequest("B3", "Broker"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await institutionsController.UpdateStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationSettingsController.Get(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationSettingsController.Update(new UpdateNotificationSettingsRequest(true,1,true,1,true,true,1,true,true,true,true,true,true,1), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        SetAuth(adminRobotsController, UserRole.Admin);
        (await adminRobotsController.Monitor(50, null, null, null, null, null, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminRobotsController.Run("ReminderRobot", false, 10, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminRobotsController.RunAll(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        adminRuntimeController.Get().Should().BeOfType<OkObjectResult>();
    }

    private static void SetAuth(ControllerBase controller, UserRole role = UserRole.Basic)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        ], "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Headers["User-Agent"] = "tests";
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    private static IFormFile BuildFormFile(string contentType)
    {
        var stream = new MemoryStream([1, 2, 3, 4]);
        return new FormFile(stream, 0, stream.Length, "file", "f.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
