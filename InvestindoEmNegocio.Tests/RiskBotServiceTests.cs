using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class RiskBotServiceTests
{
    [Fact]
    public async Task AssessAsync_Should_Return_Critical_When_Projection_Is_Negative_And_Expenses_Are_Overdue()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var projection = new CashflowProjectionResponse(
            "month",
            new DateOnly(2026, 3, 9),
            new DateOnly(2026, 3, 9),
            new DateOnly(2026, 3, 31),
            1000m,
            -150m,
            -200m,
            new DateOnly(2026, 3, 12),
            new DateOnly(2026, 3, 12),
            [new CashflowProjectionPointResponse(new DateOnly(2026, 3, 9), 1000m, 0m, 0, 0m, 0, 1000m)]);

        var projectionEngine = new Mock<ICashflowProjectionEngine>();
        projectionEngine.Setup(x => x.ProjectAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projection);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, projection.ProjectionStart, projection.ProjectionEnd, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 1, new DateOnly(2026, 3, 8), 300m),
                new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 2, new DateOnly(2026, 3, 10), 200m)
            ]);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, InstallmentStatus.Open, projection.ProjectionStart, projection.ProjectionEnd, MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, spaceId, 1, new DateOnly(2026, 3, 15), 500m)
            ]);

        var sut = new RiskBotService(installmentRepository.Object, projectionEngine.Object);

        var result = await sut.AssessAsync(userId, "month", new DateOnly(2026, 3, 9));

        result.Priority.Should().Be("critical");
        result.Classification.Should().Be("critical");
        result.Score.Should().BeLessThan(70);
        result.ReasonCodes.Should().Contain(["projected_deficit", "overdue_expenses", "pending_income"]);
        result.Recommendations.Should().NotBeEmpty();
    }
}
