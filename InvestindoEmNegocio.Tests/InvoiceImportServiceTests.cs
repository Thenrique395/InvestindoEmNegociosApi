using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

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
            Mock.Of<ICardRepository>(),
            new ImportIdentityEngine(),
            Mock.Of<ICategorizationService>(),
            Mock.Of<IRecurrenceDetectorService>(),
            Mock.Of<IInvestDbContext>(),
            NullLogger<InvoiceImportService>.Instance);
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not-a-pdf"));

        Func<Task> act = async () => await sut.ExtractAsync(Guid.NewGuid(), stream, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportAsync_Should_Create_And_Skip_Duplicated_Items()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var card = new Card(userId, spaceId, 1, "Titular", "Cartão principal", "1234", null, 1000m, 10, 20);

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

        var tx = new Mock<IDbContextTransaction>();
        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dbContext = new Mock<IInvestDbContext>();
        dbContext.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);

        var sut = new InvoiceImportService(
            new InvoiceParserFactory(),
            plansService.Object,
            installmentRepository.Object,
            planRepository.Object,
            cardRepository.Object,
            new ImportIdentityEngine(),
            Mock.Of<ICategorizationService>(),
            Mock.Of<IRecurrenceDetectorService>(),
            dbContext.Object,
            NullLogger<InvoiceImportService>.Instance);

        var request = new InvoiceImportRequest(
            CardId: cardId,
            CategoryId: null,
            DefaultDueDate: "15/03/2026",
            StatementCloseDate: null,
            InvoiceTotal: null,
            ImportIdempotencyKey: "manual-key",
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
        dbContext.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_Should_Flag_Duplicates_And_Project_Cycle_Total()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var card = new Card(userId, spaceId, 1, "Titular", "Cartão principal", "1234", null, 1000m, 10, 20);
        var cardId = card.Id;
        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Streaming", 39.90m, ScheduleType.OneTime, new DateOnly(2026, 3, 5), cardId: cardId);
        var existingInstallment = new MoneyInstallment(
            plan.Id,
            userId,
            spaceId,
            1,
            new DateOnly(2026, 3, 20),
            39.90m,
            originalDueDate: new DateOnly(2026, 3, 5),
            statementYear: 2026,
            statementMonth: 3,
            statementCloseDate: new DateOnly(2026, 3, 10),
            statementDueDate: new DateOnly(2026, 3, 20));

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository
            .Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingInstallment]);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository
            .Setup(x => x.GetByIdAsync(cardId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var sut = new InvoiceImportService(
            new InvoiceParserFactory(),
            Mock.Of<IPlansService>(),
            installmentRepository.Object,
            planRepository.Object,
            cardRepository.Object,
            new ImportIdentityEngine(),
            Mock.Of<ICategorizationService>(),
            Mock.Of<IRecurrenceDetectorService>(),
            Mock.Of<IInvestDbContext>(),
            NullLogger<InvoiceImportService>.Instance);

        var request = new InvoiceImportRequest(
            CardId: cardId,
            CategoryId: null,
            DefaultDueDate: "20/03/2026",
            StatementCloseDate: "10/03/2026",
            InvoiceTotal: "R$ 189,90",
            ImportIdempotencyKey: "manual-key",
            SkipDuplicates: true,
            Items:
            [
                new InvoiceImportItemRequest("05/03/2026", "Streaming", "R$ 39,90"),
                new InvoiceImportItemRequest("07/03/2026", "Mercado", "R$ 150,00")
            ]);

        var result = await sut.ReconcileAsync(userId, request, CancellationToken.None);

        result.TotalItems.Should().Be(2);
        result.NewItems.Should().Be(1);
        result.DuplicateItems.Should().Be(1);
        result.Items.Should().ContainSingle(x => x.Description == "Streaming" && x.IsDuplicate);
        result.Cycles.Should().ContainSingle();
        var cycle = result.Cycles[0];
        cycle.StatementReference.Should().Be("03/2026");
        cycle.CurrentTotalAmount.Should().Be(39.90m);
        cycle.ImportedNewAmount.Should().Be(150m);
        cycle.DuplicateAmount.Should().Be(39.90m);
        cycle.ProjectedTotalAmount.Should().Be(189.90m);
        cycle.DifferenceAmount.Should().Be(0m);
        cycle.ReadyToClose.Should().BeTrue();
    }
}
