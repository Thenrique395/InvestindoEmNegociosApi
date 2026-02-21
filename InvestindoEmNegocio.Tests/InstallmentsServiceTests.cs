using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class InstallmentsServiceTests
{
    [Fact]
    public async Task PayAsync_Should_Return_False_When_Installment_Not_Found()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyInstallment?)null);
        var sut = BuildSut(installmentRepository: installmentRepository);

        var result = await sut.PayAsync(Guid.NewGuid(), Guid.NewGuid(), new PaymentRequest(DateTime.UtcNow, 50));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task PayAsync_Should_Update_Status_To_PartiallyPaid_When_Total_Is_Lower_Than_Amount()
    {
        var userId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.SumPaidAmountAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var sut = BuildSut(installmentRepository: installmentRepository, paymentRepository: paymentRepository);

        var result = await sut.PayAsync(userId, installmentId, new PaymentRequest(DateTime.UtcNow, 50));

        result.Should().BeTrue();
        installment.Status.Should().Be(InstallmentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task AnticipateAsync_Should_Throw_When_Installment_Is_Current_Month()
    {
        var userId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);
        var sut = BuildSut(installmentRepository: installmentRepository);

        Func<Task> act = async () => await sut.AnticipateAsync(
            userId,
            Guid.NewGuid(),
            new AnticipationRequest(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10))));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*mês atual*");
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_Unauthorized_When_Other_User()
    {
        var installment = new MoneyInstallment(Guid.NewGuid(), Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);
        var sut = BuildSut(installmentRepository: installmentRepository);

        Func<Task> act = async () => await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static InstallmentsService BuildSut(
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null)
    {
        return new InstallmentsService(
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            NullLogger<InstallmentsService>.Instance);
    }
}
