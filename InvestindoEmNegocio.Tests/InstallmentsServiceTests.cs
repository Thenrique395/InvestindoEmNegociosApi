using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
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
    public async Task PayAsync_Should_Require_Account_When_NonBasic_User_Has_Multiple_Active_Accounts_And_None_Informed()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        user.SetRole(UserRole.Intermediate);
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Account(userId, spaceId, "Conta A", AccountType.Checking, 0m),
                new Account(userId, spaceId, "Conta B", AccountType.DigitalWallet, 0m)
            ]);

        var sut = BuildSut(
            installmentRepository: installmentRepository,
            userRepository: userRepository,
            accountRepository: accountRepository);

        Func<Task> act = async () => await sut.PayAsync(userId, installment.Id, new PaymentRequest(DateTime.UtcNow, 100m));

        await act.Should().ThrowAsync<AppProblemException>()
            .WithMessage("*Selecione a conta*");
    }

    [Fact]
    public async Task PayAsync_Should_Update_Status_To_PartiallyPaid_When_Total_Is_Lower_Than_Amount()
    {
        var userId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        // Soma dos pagamentos JÁ existentes (o novo é adicionado no serviço): 0 + 50 = 50.
        paymentRepository
            .Setup(x => x.SumPaidAmountAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = BuildSut(installmentRepository: installmentRepository, paymentRepository: paymentRepository);

        var result = await sut.PayAsync(userId, installmentId, new PaymentRequest(DateTime.UtcNow, 50));

        result.Should().BeTrue();
        installment.Status.Should().Be(InstallmentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task AnticipateAsync_Should_Throw_When_Installment_Is_Current_Month()
    {
        var userId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
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
    public async Task DeleteAsync_Should_Return_False_When_Other_User()
    {
        // Dono errado é tratado como não encontrado (return false → 404), consistente com
        // os demais métodos do serviço e sem vazar a existência da parcela alheia.
        var installment = new MoneyInstallment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);
        var sut = BuildSut(installmentRepository: installmentRepository);

        var removed = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_Should_MarkDeleted_On_Ledger_Transactions_From_Installment_Payments()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        var installmentId = installment.Id;
        var payment = new MoneyPayment(installmentId, userId, spaceId, DateTime.UtcNow, 100m);
        var transaction = new AccountTransaction(
            Guid.NewGuid(),
            userId,
            spaceId,
            DateTime.UtcNow,
            AccountTransactionKind.Debit,
            100m,
            "Pagamento parcela 1 - Plano teste",
            "InstallmentPayment",
            payment.Id);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.ListByInstallmentIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([payment]);

        var accountTransactionRepository = new Mock<IAccountTransactionRepository>();
        accountTransactionRepository
            .Setup(x => x.ListBySourceAsync(userId, "InstallmentPayment", It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transaction]);

        var sut = BuildSut(
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            accountTransactionRepository: accountTransactionRepository);

        var removed = await sut.DeleteAsync(userId, installmentId);

        removed.Should().BeTrue();
        installment.DeletedAt.Should().NotBeNull();
        payment.DeletedAt.Should().NotBeNull();
        transaction.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PayAsync_Should_Update_Status_To_Paid_When_Total_Reaches_Amount()
    {
        var userId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        // Existentes 0 + este 100 = 100 => Paid.
        paymentRepository
            .Setup(x => x.SumPaidAmountAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

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
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100);
        // Somas EXISTENTES antes de cada pagamento: 0 (antes do 1º) e 60 (antes do 2º).
        // O serviço soma o valor deste pagamento: 0+60=60 (PartiallyPaid) e 60+60=120 (Paid).
        var paidTotals = new Queue<decimal>([0m, 60m]);

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

    [Fact]
    public async Task ReversePaymentAsync_Should_Add_Reversal_Payment_And_Opposite_Ledger_Entry()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var account = new Account(userId, spaceId, "Conta principal", AccountType.Checking, 0m);
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 1, DateOnly.FromDateTime(DateTime.UtcNow.Date), 100m);
        var originalPayment = new MoneyPayment(installment.Id, userId, spaceId, DateTime.UtcNow.AddMinutes(-5), 100m, accountId: account.Id);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(originalPayment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalPayment);
        paymentRepository
            .Setup(x => x.SumPaidAmountAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.GetByIdAsync(installment.PlanId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoneyPlan(
                userId,
                spaceId,
                MoneyType.Expense,
                "Plano teste",
                100m,
                ScheduleType.OneTime,
                DateOnly.FromDateTime(DateTime.UtcNow.Date)));

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository
            .Setup(x => x.GetByIdAsync(account.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var accountTransactionRepository = new Mock<IAccountTransactionRepository>();
        accountTransactionRepository
            .Setup(x => x.ListBySourceAsync(userId, "InstallmentPaymentReversal", It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = BuildSut(
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            planRepository: planRepository,
            accountRepository: accountRepository,
            accountTransactionRepository: accountTransactionRepository);

        var result = await sut.ReversePaymentAsync(
            userId,
            installment.Id,
            originalPayment.Id,
            new PaymentReversalRequest(DateTime.UtcNow, "Ajuste manual"));

        result.Should().BeTrue();
        installment.Status.Should().Be(InstallmentStatus.Open);

        paymentRepository.Verify(x => x.AddAsync(
            It.Is<MoneyPayment>(p => p.InstallmentId == installment.Id && p.PaidAmount == -100m),
            It.IsAny<CancellationToken>()), Times.Once);

        accountTransactionRepository.Verify(x => x.AddAsync(
            It.Is<AccountTransaction>(t =>
                t.SourceType == "InstallmentPaymentReversal" &&
                t.SourceId == originalPayment.Id &&
                t.Kind == AccountTransactionKind.Credit &&
                t.Amount == 100m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttachReceiptAsync_Should_Save_File_And_Persist_Returned_Url()
    {
        var userId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var payment = new MoneyPayment(installmentId, userId, Guid.NewGuid(), DateTime.UtcNow, 100m);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var receiptStorageService = new Mock<IReceiptStorageService>();
        receiptStorageService
            .Setup(x => x.SaveAsync(userId, It.IsAny<Stream>(), "nota.pdf", "application/pdf", "https://api.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.test/uploads/receipts/abc123.pdf");

        var sut = BuildSut(paymentRepository: paymentRepository, receiptStorageService: receiptStorageService);

        using var content = new MemoryStream();
        var result = await sut.AttachReceiptAsync(userId, installmentId, payment.Id, content, "nota.pdf", "application/pdf", "https://api.test");

        result.Should().Be("https://api.test/uploads/receipts/abc123.pdf");
        payment.ReceiptUrl.Should().Be("https://api.test/uploads/receipts/abc123.pdf");
        paymentRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttachReceiptAsync_Should_Return_Null_When_Payment_Not_Found()
    {
        var userId = Guid.NewGuid();
        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyPayment?)null);

        var sut = BuildSut(paymentRepository: paymentRepository);

        using var content = new MemoryStream();
        var result = await sut.AttachReceiptAsync(userId, Guid.NewGuid(), Guid.NewGuid(), content, "nota.pdf", "application/pdf", "https://api.test");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AttachReceiptAsync_Should_Return_Null_When_Payment_Belongs_To_Different_Installment()
    {
        var userId = Guid.NewGuid();
        var payment = new MoneyPayment(Guid.NewGuid(), userId, Guid.NewGuid(), DateTime.UtcNow, 100m);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var receiptStorageService = new Mock<IReceiptStorageService>();
        var sut = BuildSut(paymentRepository: paymentRepository, receiptStorageService: receiptStorageService);

        using var content = new MemoryStream();
        var result = await sut.AttachReceiptAsync(userId, Guid.NewGuid(), payment.Id, content, "nota.pdf", "application/pdf", "https://api.test");

        result.Should().BeNull();
        receiptStorageService.Verify(x => x.SaveAsync(
            It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_False_When_Not_Found()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyInstallment?)null);
        var sut = BuildSut(installmentRepository: installmentRepository);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateInstallmentRequest(100m, new DateOnly(2026, 8, 1)));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_Should_Edit_Amount_And_DueDate_Preserving_Payments()
    {
        var userId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, new DateOnly(2026, 7, 10), 100m);
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);
        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.ListByInstallmentIdAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MoneyPayment(installment.Id, userId, installment.SpaceId, DateTime.UtcNow, 60m, null, null, null)]);
        var sut = BuildSut(installmentRepository: installmentRepository, paymentRepository: paymentRepository);

        var newDue = new DateOnly(2026, 8, 15);
        var result = await sut.UpdateAsync(userId, installment.Id, new UpdateInstallmentRequest(150m, newDue));

        result.Should().BeTrue();
        installment.Amount.Should().Be(150m);
        installment.DueDate.Should().Be(newDue);
        installment.Status.Should().Be(InstallmentStatus.PartiallyPaid);
        installmentRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Installment_Anticipated()
    {
        var userId = Guid.NewGuid();
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, Guid.NewGuid(), 1, new DateOnly(2026, 7, 10), 100m);
        installment.Anticipate(new DateOnly(2026, 6, 10), new DateOnly(2026, 5, 1));
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.GetByIdAsync(installment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installment);
        var sut = BuildSut(installmentRepository: installmentRepository);

        Func<Task> act = async () => await sut.UpdateAsync(userId, installment.Id, new UpdateInstallmentRequest(120m, new DateOnly(2026, 9, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*antecipada*");
    }

    private static InstallmentsService BuildSut(
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountTransactionRepository>? accountTransactionRepository = null,
        Mock<IReceiptStorageService>? receiptStorageService = null)
    {
        var userRepo = userRepository ?? CreateDefaultUserRepository();
        var accountRepo = accountRepository ?? CreateDefaultAccountRepository();

        var effectivePlanRepository = planRepository ?? new Mock<IMoneyPlanRepository>();
        effectivePlanRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid userId, CancellationToken _) =>
                new MoneyPlan(
                    userId,
                    Guid.NewGuid(),
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
            receiptStorageService?.Object ?? Mock.Of<IReceiptStorageService>(),
            NullLogger<InstallmentsService>.Instance);
    }

    private static Mock<IUserRepository> CreateDefaultUserRepository()
    {
        var defaultUser = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultUser);
        return userRepo;
    }

    private static Mock<IAccountRepository> CreateDefaultAccountRepository()
    {
        var accountRepo = new Mock<IAccountRepository>();
        accountRepo
            .Setup(x => x.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) => [new Account(userId, Guid.NewGuid(), "Conta principal", AccountType.Checking, 0m)]);
        return accountRepo;
    }
}
