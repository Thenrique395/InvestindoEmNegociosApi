using System.Security.Claims;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestindoEmNegocio.Tests;

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
        var audit = new Mock<IAuditService>();
        var c = new CardsController(cards.Object, audit.Object);
        SetAuth(c);

        var req = new CardRequest(1, "User", "Cartao", "1234", null, 1000m, 10, 20);
        (await c.List(new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Create(req, CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await c.Update(Guid.NewGuid(), req, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await c.GetTotalDebt(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CategoriesController_Should_Cover_Main_Actions_And_Error_Mapping()
    {
        var service = new Mock<ICategoriesService>();
        var categoryResponse = new CategoryResponse(Guid.NewGuid(), "Cat", MoneyType.Expense, false);
        service.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<MoneyType?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CategoryResponse>());
        service.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<UpsertCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(categoryResponse);
        service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpsertCategoryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(categoryResponse);
        service.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var c = new CategoriesController(service.Object, Mock.Of<IAuditService>());
        SetAuth(c);

        (await c.List(null, new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Create(new UpsertCategoryRequest("Cat", MoneyType.Expense), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Update(Guid.NewGuid(), new UpsertCategoryRequest("Cat", MoneyType.Expense), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

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
        SetAuth(c);

        (await c.List(null, null, null, null, new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await c.Pay(Guid.NewGuid(), new PaymentRequest(DateTime.UtcNow, 10m), CancellationToken.None)).Should().BeOfType<OkResult>();
        (await c.Anticipate(Guid.NewGuid(), new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None)).Should().BeOfType<OkResult>();
        (await c.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        service.Setup(x => x.AnticipateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AnticipationRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("x"));
        Func<Task> anticipateBad = async () => await c.Anticipate(Guid.NewGuid(), new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);
        await anticipateBad.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Lightweight_Controllers_Should_Return_Ok_Or_NoContent()
    {
        var notifications = new Mock<INotificationsService>();
        notifications.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<NotificationDto>());
        notifications.Setup(x => x.GenerateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var notificationsController = new NotificationsController(notifications.Object);
        SetAuth(notificationsController);
        (await notificationsController.List(false, 10, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationsController.Generate(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await notificationsController.MarkRead(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        var lookups = new Mock<ILookupsService>();
        lookups.Setup(x => x.GetPaymentMethodsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PaymentMethod>());
        lookups.Setup(x => x.GetCardBrandsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CardBrand>());
        lookups.Setup(x => x.GetInstitutionsAsync(It.IsAny<InstitutionType?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Institution>());
        var lookupsController = new LookupsController(lookups.Object);
        (await lookupsController.GetPaymentMethods(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await lookupsController.GetCardBrands(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await lookupsController.GetInstitutions("Bank", CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var prefs = new Mock<IPreferencesService>();
        prefs.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PreferencesDto)!);
        prefs.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdatePreferencesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(PreferencesDto)!);
        var prefsController = new PreferencesController(prefs.Object);
        SetAuth(prefsController);
        (await prefsController.Get(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await prefsController.Update(new UpdatePreferencesRequest("BRL", [], null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var onboarding = new Mock<IOnboardingService>();
        onboarding.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(OnboardingStatusDto)!);
        onboarding.Setup(x => x.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<UpdateOnboardingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(OnboardingStatusDto)!);
        var onboardingController = new OnboardingController(onboarding.Object);
        SetAuth(onboardingController);
        (await onboardingController.Get(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await onboardingController.Update(new UpdateOnboardingRequest(1, true), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DataPortability_Profile_And_GoalContributions_Should_Cover_Main_Flows()
    {
        var dp = new Mock<IDataPortabilityFacadeService>();
        dp.Setup(x => x.ExportAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(("export.json", "{}"u8.ToArray()));
        dp.Setup(x => x.ImportAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ImportUserDataResult(1));
        var dpController = new DataPortabilityController(dp.Object);
        SetAuth(dpController);
        (await dpController.Export(CancellationToken.None)).Should().BeOfType<FileContentResult>();
        var importFile = BuildFormFile("application/json");
        (await dpController.Import(new ImportUserDataRequest { File = importFile, ReplaceExisting = false }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        Func<Task> missingImport = async () => await dpController.Import(new ImportUserDataRequest(), CancellationToken.None);
        await missingImport.Should().ThrowAsync<AppProblemException>();

        var profileService = new Mock<IProfileService>();
        profileService.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserProfileDto?)null);
        profileService.Setup(x => x.UpsertAsync(It.IsAny<Guid>(), It.IsAny<UpsertUserProfileRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserProfileDto)!);
        profileService.Setup(x => x.UpdateAvatarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(default(UserProfileDto)!);
        var avatar = new Mock<IAvatarStorageService>();
        avatar.Setup(x => x.SaveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("https://x");
        var profileController = new ProfileController(profileService.Object, avatar.Object);
        SetAuth(profileController);
        profileController.ControllerContext.HttpContext.Request.Scheme = "https";
        profileController.ControllerContext.HttpContext.Request.Host = new HostString("example.com");
        (await profileController.Get(CancellationToken.None)).Result.Should().BeOfType<NoContentResult>();
        (await profileController.Upsert(new UpsertUserProfileRequest("A", "1", "2", null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "pt-BR"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await profileController.UploadAvatar(new UploadAvatarRequest { Avatar = BuildFormFile("image/png") }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();

        var contrib = new Mock<IGoalContributionsService>();
        var contribution = new GoalContributionResponse(Guid.NewGuid(), 10m, DateOnly.FromDateTime(DateTime.UtcNow), null, DateTime.UtcNow);
        contrib.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<GoalContributionResponse>());
        contrib.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<GoalContributionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(contribution);
        var contribController = new GoalContributionsController(contrib.Object);
        SetAuth(contribController);
        (await contribController.List(Guid.NewGuid(), new ListQuery(1, 10, null, null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await contribController.Create(Guid.NewGuid(), new GoalContributionRequest(10m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

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
        var adminParametersController = new AdminParametersController(adminParameters.Object);
        (await adminParametersController.ListPaymentMethods(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.UpdatePaymentMethodStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.CreatePaymentMethod(new CreatePaymentMethodRequest("Pix"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.ListCardBrands(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.UpdateCardBrandStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.CreateCardBrand(new CreateCardBrandRequest("Visa", "VISA"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.ListInstitutions(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.CreateInstitution(new CreateInstitutionRequest("B3", "Broker"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.UpdateInstitutionStatus(1, new UpdateActiveRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.GetNotificationSettings(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await adminParametersController.UpdateNotificationSettings(new UpdateNotificationSettingsRequest(true,1,true,1,true,true,1,true,true,true,true,true,true,1), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    private static void SetAuth(ControllerBase controller)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test");
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
