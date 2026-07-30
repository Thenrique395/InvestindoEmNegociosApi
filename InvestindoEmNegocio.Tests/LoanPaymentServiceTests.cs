using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LoanPaymentServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    private LoanContract BuildContract() => new(
        _userId, _spaceId, "Empréstimo", 5000m, 12m, 0.01m, InterestRatePeriod.AnnualNominal, 2,
        LoanAmortizationType.Price, new DateOnly(2026, 1, 10), 10, 2600m, 5200m, 200m, 5200m);

    private LoanInstallment BuildInstallment(Guid contractId, int no, decimal total = 2600m, decimal principal = 2500m, decimal interest = 100m)
        => new(contractId, _userId, no, new DateOnly(2026, no, 10), 5000m, principal, interest, total, 5000m - principal);

    private sealed record Sut(
        LoanPaymentService Service,
        Mock<ILoanContractRepository> Contracts,
        Mock<ILoanInstallmentRepository> Installments,
        Mock<ILoanPaymentRepository> Payments,
        Mock<IAccountRepository> Accounts,
        Mock<IAccountTransactionRepository> Transactions);

    private Sut BuildSut(LoanContract contract, List<LoanInstallment> installments)
    {
        var contracts = new Mock<ILoanContractRepository>();
        contracts.Setup(x => x.GetByIdAsync(contract.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

        var insts = new Mock<ILoanInstallmentRepository>();
        foreach (var i in installments)
            insts.Setup(x => x.GetByIdAsync(i.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(i);
        insts.Setup(x => x.ListByContractAsync(contract.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(installments);

        var payments = new Mock<ILoanPaymentRepository>();
        payments.Setup(x => x.GetByIdempotencyKeyAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LoanPayment?)null);

        var accounts = new Mock<IAccountRepository>();
        var txns = new Mock<IAccountTransactionRepository>();

        var svc = new LoanPaymentService(contracts.Object, insts.Object, payments.Object, accounts.Object, txns.Object,
            Mock.Of<InvestindoEmNegocio.Application.Interfaces.IReceiptStorageService>(), Mock.Of<ILogger<LoanPaymentService>>());
        return new Sut(svc, contracts, insts, payments, accounts, txns);
    }

    [Fact]
    public async Task Pay_Normal_Without_Account_Records_Payment_And_Marks_Installment_Paid()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var i2 = BuildInstallment(contract.Id, 2);
        var sut = BuildSut(contract, [i1, i2]);

        var result = await sut.Service.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow));

        result.Amount.Should().Be(2600m);
        i1.Status.Should().Be(LoanInstallmentStatus.Paid);
        sut.Payments.Verify(x => x.AddAsync(It.IsAny<LoanPayment>(), It.IsAny<CancellationToken>()), Times.Once);
        sut.Payments.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        sut.Transactions.Verify(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        contract.Status.Should().Be(LoanStatus.Active);
        result.Contract.OpenInstallments.Should().Be(1);
    }

    [Fact]
    public async Task Pay_With_Account_Creates_Debit_Transaction_Linked_To_Payment()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var i2 = BuildInstallment(contract.Id, 2);
        var sut = BuildSut(contract, [i1, i2]);
        var account = new Account(_userId, _spaceId, "Conta Corrente", default);
        sut.Accounts.Setup(x => x.GetByIdAsync(account.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        AccountTransaction? captured = null;
        sut.Transactions.Setup(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AccountTransaction, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var result = await sut.Service.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow, AccountId: account.Id));

        captured.Should().NotBeNull();
        captured!.Kind.Should().Be(AccountTransactionKind.Debit);
        captured.Amount.Should().Be(2600m);
        captured.SourceType.Should().Be(AccountTransactionSourceTypes.LoanPayment);
        result.AccountTransactionId.Should().Be(captured.Id);
    }

    [Fact]
    public async Task Pay_Is_Idempotent_When_Key_Already_Used()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var sut = BuildSut(contract, [i1]);
        var existing = new LoanPayment(contract.Id, i1.Id, _userId, _spaceId, DateTime.UtcNow, 2600m, 2500m, 100m, "key-123");
        sut.Payments.Setup(x => x.GetByIdempotencyKeyAsync(_userId, "key-123", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await sut.Service.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow, IdempotencyKey: "key-123"));

        result.PaymentId.Should().Be(existing.Id);
        // Nenhum novo pagamento/movimentação é criado no replay.
        sut.Payments.Verify(x => x.AddAsync(It.IsAny<LoanPayment>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.Payments.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        sut.Transactions.Verify(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pay_Last_Installment_Closes_Contract()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        i1.MarkPaid(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc));
        var i2 = BuildInstallment(contract.Id, 2);
        var sut = BuildSut(contract, [i1, i2]);

        var result = await sut.Service.PayAsync(_userId, contract.Id, i2.Id, new LoanPaymentRequest(DateTime.UtcNow));

        contract.Status.Should().Be(LoanStatus.Closed);
        contract.ClosedAt.Should().NotBeNull();
        result.Contract.Status.Should().Be(LoanStatus.Closed);
        result.Contract.OpenInstallments.Should().Be(0);
    }

    [Fact]
    public async Task Pay_Applies_Penalty_And_Discount_To_Amount()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var sut = BuildSut(contract, [i1]);

        var result = await sut.Service.PayAsync(_userId, contract.Id, i1.Id,
            new LoanPaymentRequest(DateTime.UtcNow, PenaltyAmount: 50m, DiscountAmount: 20m));

        result.Amount.Should().Be(2630m); // 2600 + 50 - 20
        result.PenaltyAmount.Should().Be(50m);
        result.DiscountAmount.Should().Be(20m);
        i1.PaidAmount.Should().Be(2630m);
    }

    [Fact]
    public async Task Pay_Rejects_Installment_From_Another_Contract()
    {
        var contract = BuildContract();
        var foreign = BuildInstallment(Guid.NewGuid(), 1); // ContractId diferente
        var sut = BuildSut(contract, [foreign]);

        (await sut.Service.Invoking(x => x.PayAsync(_userId, contract.Id, foreign.Id, new LoanPaymentRequest(DateTime.UtcNow)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Pay_Rejects_When_Contract_Not_Active()
    {
        var contract = BuildContract();
        contract.Archive();
        var i1 = BuildInstallment(contract.Id, 1);
        var sut = BuildSut(contract, [i1]);

        (await sut.Service.Invoking(x => x.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("loan_not_active");
    }

    [Fact]
    public async Task Pay_Rejects_When_Installment_Already_Paid()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        i1.MarkPaid(DateTime.UtcNow);
        var sut = BuildSut(contract, [i1]);

        (await sut.Service.Invoking(x => x.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("installment_already_paid");
    }

    [Fact]
    public async Task Pay_Rejects_When_Account_Not_Owned()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var sut = BuildSut(contract, [i1]);
        var foreignAccountId = Guid.NewGuid();
        sut.Accounts.Setup(x => x.GetByIdAsync(foreignAccountId, _userId, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        (await sut.Service.Invoking(x => x.PayAsync(_userId, contract.Id, i1.Id, new LoanPaymentRequest(DateTime.UtcNow, AccountId: foreignAccountId)))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("invalid_account");
    }

    [Fact]
    public async Task Reverse_Credits_Account_And_Reopens_Installment_Preserving_Payment()
    {
        var utc = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc);
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        i1.MarkPaid(utc);
        var i2 = BuildInstallment(contract.Id, 2);
        var account = new Account(_userId, _spaceId, "Conta", default);
        var payment = new LoanPayment(contract.Id, i1.Id, _userId, _spaceId, utc, 2600m, 2500m, 100m, "k1", accountId: account.Id);
        var sut = BuildSut(contract, [i1, i2]);
        sut.Payments.Setup(x => x.GetByIdAsync(payment.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        sut.Accounts.Setup(x => x.GetByIdAsync(account.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        AccountTransaction? captured = null;
        sut.Transactions.Setup(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AccountTransaction, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        await sut.Service.ReverseAsync(_userId, contract.Id, i1.Id, payment.Id, new LoanPaymentReversalRequest("lançado errado"));

        i1.Status.Should().Be(LoanInstallmentStatus.Open);
        payment.IsReversed.Should().BeTrue("o pagamento é preservado, apenas marcado como estornado");
        payment.ReversalReason.Should().Be("lançado errado");
        captured.Should().NotBeNull();
        captured!.Kind.Should().Be(AccountTransactionKind.Credit);
        captured.Amount.Should().Be(2600m);
        captured.SourceType.Should().Be(AccountTransactionSourceTypes.LoanPaymentReversal);
        sut.Payments.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reverse_Last_Payment_Reopens_Closed_Contract()
    {
        var utc = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc);
        var contract = BuildContract();
        contract.MarkClosed();
        var i1 = BuildInstallment(contract.Id, 1);
        i1.MarkPaid(utc);
        var payment = new LoanPayment(contract.Id, i1.Id, _userId, _spaceId, utc, 2600m, 2500m, 100m, "k2");
        var sut = BuildSut(contract, [i1]);
        sut.Payments.Setup(x => x.GetByIdAsync(payment.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var result = await sut.Service.ReverseAsync(_userId, contract.Id, i1.Id, payment.Id, new LoanPaymentReversalRequest());

        contract.Status.Should().Be(LoanStatus.Active);
        contract.ClosedAt.Should().BeNull();
        result.Contract.Status.Should().Be(LoanStatus.Active);
        result.Contract.OpenInstallments.Should().Be(1);
    }

    [Fact]
    public async Task Reverse_Rejects_When_Already_Reversed()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var payment = new LoanPayment(contract.Id, i1.Id, _userId, _spaceId, DateTime.UtcNow, 2600m, 2500m, 100m, "k3");
        payment.MarkReversed(DateTime.UtcNow, "já");
        var sut = BuildSut(contract, [i1]);
        sut.Payments.Setup(x => x.GetByIdAsync(payment.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        (await sut.Service.Invoking(x => x.ReverseAsync(_userId, contract.Id, i1.Id, payment.Id, new LoanPaymentReversalRequest()))
            .Should().ThrowAsync<AppProblemException>())
            .Which.Code.Should().Be("payment_already_reversed");
    }

    [Fact]
    public async Task Reverse_Rejects_When_Payment_Does_Not_Match_Contract()
    {
        var contract = BuildContract();
        var i1 = BuildInstallment(contract.Id, 1);
        var payment = new LoanPayment(Guid.NewGuid(), i1.Id, _userId, _spaceId, DateTime.UtcNow, 2600m, 2500m, 100m, "k4");
        var sut = BuildSut(contract, [i1]);
        sut.Payments.Setup(x => x.GetByIdAsync(payment.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        (await sut.Service.Invoking(x => x.ReverseAsync(_userId, contract.Id, i1.Id, payment.Id, new LoanPaymentReversalRequest()))
            .Should().ThrowAsync<AppProblemException>())
            .Which.StatusCode.Should().Be(400);
    }
}
