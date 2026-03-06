using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Tests;

public class InvoiceImportServiceTests
{
    [Fact]
    public async Task ExtractAsync_Should_Throw_When_Stream_Is_Not_A_Valid_Pdf()
    {
        var sut = new InvoiceImportService(
            new InvoiceParserFactory(),
            Mock.Of<IPlansService>(),
            Mock.Of<IMoneyInstallmentRepository>(),
            Mock.Of<IMoneyPlanRepository>(),
            Mock.Of<ICardRepository>());
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not-a-pdf"));

        Func<Task> act = async () => await sut.ExtractAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportAsync_Should_Create_And_Skip_Duplicated_Items()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card(userId, 1, "Titular", "Cartão principal", "1234", null, 1000m, 10, 20);

        var plansService = new Mock<IPlansService>();
        plansService
            .Setup(x => x.CreateAsync(userId, It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CreatePlanRequest req, CancellationToken _) =>
                new PlanResponse(Guid.NewGuid(), req.Type, req.Title, req.Amount, req.Schedule, req.Frequency, req.InstallmentsCount, req.StartDate, "Active", req.CategoryId, req.CardId));

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var sut = new InvoiceImportService(
            new InvoiceParserFactory(),
            plansService.Object,
            installmentRepository.Object,
            planRepository.Object,
            cardRepository.Object);

        var request = new InvoiceImportRequest(
            CardId: cardId,
            CategoryId: null,
            DefaultDueDate: "15/03/2026",
            SkipDuplicates: true,
            Items:
            [
                new InvoiceImportItemRequest("05/03/2026", "Streaming", "R$ 39,90"),
                new InvoiceImportItemRequest("05/03/2026", "Streaming", "R$ 39,90"),
                new InvoiceImportItemRequest("07/03/2026", "Mercado", "R$ 150,00")
            ]);

        var result = await sut.ImportAsync(userId, request, CancellationToken.None);

        result.Created.Should().Be(2);
        result.Skipped.Should().Be(1);
        result.Failed.Should().Be(0);
        plansService.Verify(x => x.CreateAsync(userId, It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
