using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class CardsServiceTests
{
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Brand_Does_Not_Exist()
    {
        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = BuildSut(brandRepository: brandRepository);

        var request = NewRequest();
        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*BrandId*");
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_And_Return_Response()
    {
        var userId = Guid.NewGuid();
        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var cardRepository = new Mock<ICardRepository>();
        var sut = BuildSut(cardRepository, brandRepository);

        var result = await sut.CreateAsync(userId, NewRequest());

        result.BrandId.Should().Be(1);
        result.Last4.Should().Be("1234");
        cardRepository.Verify(x => x.AddAsync(It.IsAny<Card>(), It.IsAny<CancellationToken>()), Times.Once);
        cardRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_Conflict_When_Nickname_Already_Exists()
    {
        var userId = Guid.NewGuid();
        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.NicknameExistsAsync(userId, "Principal", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = BuildSut(cardRepository, brandRepository);

        Func<Task> act = async () => await sut.CreateAsync(userId, NewRequest());

        await act.Should().ThrowAsync<AppProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
        cardRepository.Verify(x => x.AddAsync(It.IsAny<Card>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Card_Not_Found()
    {
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Card?)null);
        var sut = BuildSut(cardRepository: cardRepository);

        var updated = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), NewRequest());

        updated.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Brand_Does_Not_Exist()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Card(userId, 1, "Nome", "Apelido", "1234", "Banco", 1000, 10, 20));

        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildSut(cardRepository, brandRepository);

        Func<Task> act = async () => await sut.UpdateAsync(userId, cardId, NewRequest());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*BrandId*");
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_Card_And_Return_Response()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var existing = new Card(userId, 1, "Nome antigo", "Antigo", "0000", "Banco antigo", 1200, 5, 15);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildSut(cardRepository, brandRepository);

        var updated = await sut.UpdateAsync(userId, cardId, NewRequest());

        updated.Should().NotBeNull();
        updated!.HolderName.Should().Be("Henrique Santos");
        updated.Bank.Should().Be("Banco X");
        cardRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_Conflict_When_Nickname_Already_Exists_For_Another_Card()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var existing = new Card(userId, 1, "Nome antigo", "Antigo", "0000", "Banco antigo", 1200, 5, 15);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        cardRepository
            .Setup(x => x.NicknameExistsAsync(userId, "Principal", cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildSut(cardRepository, brandRepository);

        Func<Task> act = async () => await sut.UpdateAsync(userId, cardId, NewRequest());

        await act.Should().ThrowAsync<AppProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
        cardRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_Conflict_When_Save_Fails_With_DbUpdateException()
    {
        var userId = Guid.NewGuid();
        var brandRepository = new Mock<ICardBrandRepository>();
        brandRepository
            .Setup(x => x.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.NicknameExistsAsync(userId, "Principal", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        cardRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException());
        var sut = BuildSut(cardRepository, brandRepository);

        Func<Task> act = async () => await sut.CreateAsync(userId, NewRequest());

        await act.Should().ThrowAsync<AppProblemException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ListAsync_Should_Map_Repository_Result()
    {
        var userId = Guid.NewGuid();
        var cards = new List<Card>
        {
            new(userId, 1, "Nome 1", "Apelido 1", "1111", "Banco 1", 1000, 10, 20),
            new(userId, 2, "Nome 2", "Apelido 2", "2222", "Banco 2", 2000, 11, 21)
        };

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cards);

        var sut = BuildSut(cardRepository: cardRepository);

        var result = await sut.ListAsync(userId);

        result.Should().HaveCount(2);
        result.Select(x => x.Last4).Should().Contain(["1111", "2222"]);
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_False_When_Card_Not_Found()
    {
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Card?)null);
        var sut = BuildSut(cardRepository: cardRepository);

        var removed = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        removed.Should().BeFalse();
        cardRepository.Verify(x => x.Remove(It.IsAny<Card>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Should_MarkDeleted_And_Save_When_Card_Exists()
    {
        var userId = Guid.NewGuid();
        var card = new Card(userId, 1, "Nome", "Apelido", "1234", "Banco", 1000, 10, 20);
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var sut = BuildSut(cardRepository: cardRepository);

        var removed = await sut.DeleteAsync(userId, Guid.NewGuid());

        removed.Should().BeTrue();
        card.DeletedAt.Should().NotBeNull();
        cardRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTotalDebtAsync_Should_Return_Value_From_Repository()
    {
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.SumCardDebtAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(321.55m);
        var sut = BuildSut(installmentRepository: installmentRepository);

        var total = await sut.GetTotalDebtAsync(Guid.NewGuid());

        total.Should().Be(321.55m);
    }

    [Fact]
    public async Task ListStatementCyclesAsync_Should_Return_Null_When_Card_Not_Found()
    {
        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Card?)null);
        var sut = BuildSut(cardRepository: cardRepository);

        var result = await sut.ListStatementCyclesAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListStatementCyclesAsync_Should_Consolidate_Cycle()
    {
        var userId = Guid.NewGuid();
        var card = new Card(userId, 1, "Nome", "Principal", "1234", "Banco", 5000m, 10, 20);
        var cardId = card.Id;

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var plan = new MoneyPlan(userId, MoneyType.Expense, "Internet", 120m, ScheduleType.OneTime, new DateOnly(2026, 2, 5), null, 1, null, null, cardId);
        var planId = plan.Id;
        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var installment = new MoneyInstallment(planId, userId, 1, new DateOnly(2026, 3, 20), 120m, null, 2026, 3, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20));
        var installmentId = installment.Id;
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository
            .Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MoneyPayment(installmentId, userId, DateTime.UtcNow, 20m)]);

        var sut = BuildSut(
            cardRepository: cardRepository,
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            planRepository: planRepository);

        var result = await sut.ListStatementCyclesAsync(userId, cardId, 2026, 3);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].TotalAmount.Should().Be(120m);
        result[0].TotalPaid.Should().Be(20m);
        result[0].TotalOpen.Should().Be(100m);
    }

    private static CardRequest NewRequest() =>
        new(
            BrandId: 1,
            HolderName: "Henrique Santos",
            Last4: "1234",
            Nickname: "Principal",
            Bank: "Banco X",
            CreditLimit: 5000,
            StatementCloseDay: 10,
            DueDay: 20);

    private static CardsService BuildSut(
        Mock<ICardRepository>? cardRepository = null,
        Mock<ICardBrandRepository>? brandRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null)
    {
        return new CardsService(
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            brandRepository?.Object ?? Mock.Of<ICardBrandRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            NullLogger<CardsService>.Instance);
    }
}
