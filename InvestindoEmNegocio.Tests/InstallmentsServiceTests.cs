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

    [Fact]
    public async Task PayAsync_Should_Update_Status_To_Paid_When_Total_Reaches_Amount()
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
            .ReturnsAsync(100);

        var sut = BuildSut(installmentRepository: installmentRepository, paymentRepository: paymentRepository);

        var result = await sut.PayAsync(userId, installmentId, new PaymentRequest(DateTime.UtcNow, 100));

        result.Should().BeTrue();
        installment.Status.Should().Be(InstallmentStatus.Paid);
    }

    [Fact]
    public async Task PayAsync_Should_Handle_Double_Payment_And_Keep_Status_Paid()
    {
        var userId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        var paidTotals = new Queue<decimal>([60m, 120m]);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.SumPaidAmountAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => paidTotals.Dequeue());

        var sut = BuildSut(installmentRepository: installmentRepository, paymentRepository: paymentRepository);

        var first = await sut.PayAsync(userId, installmentId, new PaymentRequest(DateTime.UtcNow, 60));
        var second = await sut.PayAsync(userId, installmentId, new PaymentRequest(DateTime.UtcNow, 60));

        first.Should().BeTrue();
        second.Should().BeTrue();
        installment.Status.Should().Be(InstallmentStatus.Paid);
        paymentRepository.Verify(x => x.AddAsync(It.IsAny<MoneyPayment>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static InstallmentsService BuildSut(
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountTransactionRepository>? accountTransactionRepository = null)
    {
        var defaultUser = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        var userRepo = userRepository ?? new Mock<IUserRepository>();
        userRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultUser);

        var accountRepo = accountRepository ?? new Mock<IAccountRepository>();
        accountRepo
            .Setup(x => x.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) => [new Account(userId, "Conta principal", AccountType.Checking, 0m)]);

        var effectivePlanRepository = planRepository ?? new Mock<IMoneyPlanRepository>();
        effectivePlanRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid userId, CancellationToken _) =>
                new MoneyPlan(
                    userId,
                    MoneyType.Expense,
                    "Plano teste",
                    100m,
                    ScheduleType.OneTime,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date)));

        return new InstallmentsService(
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            effectivePlanRepository.Object,
            userRepo.Object,
            accountRepo.Object,
            accountTransactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            NullLogger<InstallmentsService>.Instance);
    }
}
