using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AdminParametersServiceTests
{
    [Fact]
    public async Task CreatePaymentMethodAsync_Should_Throw_When_Name_Already_Exists()
    {
        var paymentMethodRepository = new Mock<IPaymentMethodRepository>();
        paymentMethodRepository
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PaymentMethod(1, "PIX")]);

        var sut = BuildSut(paymentMethodRepository: paymentMethodRepository);

        Func<Task> act = async () => await sut.CreatePaymentMethodAsync("pix", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateInstitutionAsync_Should_Throw_When_Type_Is_Invalid()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.CreateInstitutionAsync(new CreateInstitutionRequest("Banco X", "invalid"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreatePaymentMethodAsync_Should_Throw_When_Name_Is_Empty()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.CreatePaymentMethodAsync("   ", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateCardBrandAsync_Should_Throw_When_Name_Or_Code_Is_Empty()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.CreateCardBrandAsync(new CreateCardBrandRequest("", " "), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateInstitutionAsync_Should_Throw_When_Name_Is_Empty()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.CreateInstitutionAsync(new CreateInstitutionRequest(" ", "Bank"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateNotificationSettingsAsync_Should_Save_And_Return_Updated_Settings()
    {
        var settings = new NotificationSettings(
            true,
            2,
            true,
            2,
            true,
            true,
            2,
            true,
            true,
            true,
            true,
            true,
            true,
            30);

        var notificationSettingsRepository = new Mock<INotificationSettingsRepository>();
        notificationSettingsRepository
            .Setup(x => x.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = BuildSut(notificationSettingsRepository: notificationSettingsRepository);

        var result = await sut.UpdateNotificationSettingsAsync(
            new UpdateNotificationSettingsRequest(
                false,
                5,
                false,
                6,
                true,
                false,
                7,
                false,
                true,
                false,
                true,
                false,
                true,
                15),
            CancellationToken.None);

        result.IncomeUpcomingEnabled.Should().BeFalse();
        result.CardCloseDaysBefore.Should().Be(7);
        result.GoalInactivityDays.Should().Be(15);
        notificationSettingsRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentMethodAsync_Should_Throw_AppProblem_When_Save_Fails()
    {
        var paymentMethodRepository = new Mock<IPaymentMethodRepository>();
        paymentMethodRepository
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        paymentMethodRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db error"));

        var sut = BuildSut(paymentMethodRepository: paymentMethodRepository);

        Func<Task> act = async () => await sut.CreatePaymentMethodAsync("PIX", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateCardBrandAsync_Should_Throw_AppProblem_When_Save_Fails()
    {
        var cardBrandRepository = new Mock<ICardBrandRepository>();
        cardBrandRepository
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        cardBrandRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db error"));

        var sut = BuildSut(cardBrandRepository: cardBrandRepository);

        Func<Task> act = async () => await sut.CreateCardBrandAsync(new CreateCardBrandRequest("Visa", "visa"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateInstitutionAsync_Should_Throw_AppProblem_When_Save_Fails()
    {
        var institutionRepository = new Mock<IInstitutionRepository>();
        institutionRepository
            .Setup(x => x.ExistsAsync("BANCO X", InstitutionType.Bank, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        institutionRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db error"));

        var sut = BuildSut(institutionRepository: institutionRepository);

        Func<Task> act = async () => await sut.CreateInstitutionAsync(new CreateInstitutionRequest("Banco X", "Bank"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task SendTestEmailAsync_Should_Send_When_Email_Is_Valid()
    {
        var emailSender = new Mock<IEmailSender>();
        var sut = BuildSut(emailSender: emailSender);

        var result = await sut.SendTestEmailAsync("admin@teste.com", CancellationToken.None);

        result.To.Should().Be("admin@teste.com");
        emailSender.Verify(x => x.SendAsync("admin@teste.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTestEmailAsync_Should_Throw_When_Email_Is_Invalid()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.SendTestEmailAsync("invalido", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SendTestEmailAsync_Should_Throw_When_Sender_Fails()
    {
        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponível"));
        var sut = BuildSut(emailSender: emailSender);

        Func<Task> act = async () => await sut.SendTestEmailAsync("admin@teste.com", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(503);
    }

    private static AdminParametersService BuildSut(
        Mock<IPaymentMethodRepository>? paymentMethodRepository = null,
        Mock<ICardBrandRepository>? cardBrandRepository = null,
        Mock<IInstitutionRepository>? institutionRepository = null,
        Mock<INotificationSettingsRepository>? notificationSettingsRepository = null,
        Mock<IRobotSettingsRepository>? robotSettingsRepository = null,
        Mock<IEmailSender>? emailSender = null)
    {
        var adminPaymentMethodsService = new AdminPaymentMethodsService(
            paymentMethodRepository?.Object ?? Mock.Of<IPaymentMethodRepository>());
        var adminCardBrandsService = new AdminCardBrandsService(
            cardBrandRepository?.Object ?? Mock.Of<ICardBrandRepository>());
        var adminInstitutionsService = new AdminInstitutionsService(
            institutionRepository?.Object ?? Mock.Of<IInstitutionRepository>());
        var adminNotificationSettingsService = new AdminNotificationSettingsService(
            notificationSettingsRepository?.Object ?? Mock.Of<INotificationSettingsRepository>());
        var adminRobotSettingsService = new AdminRobotSettingsService(
            robotSettingsRepository?.Object ?? Mock.Of<IRobotSettingsRepository>());
        var adminEmailDiagnosticsService = new AdminEmailDiagnosticsService(
            emailSender?.Object ?? Mock.Of<IEmailSender>());

        return new AdminParametersService(
            adminPaymentMethodsService,
            adminCardBrandsService,
            adminInstitutionsService,
            adminNotificationSettingsService,
            adminRobotSettingsService,
            adminEmailDiagnosticsService);
    }
}
