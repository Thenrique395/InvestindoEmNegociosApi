using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_Should_Add_And_Save_Audit_Entry()
    {
        AuditLog? captured = null;
        var repository = new Mock<IAuditLogRepository>();
        repository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((audit, _) => captured = audit)
            .Returns(Task.CompletedTask);

        var sut = new AuditService(repository.Object);

        await sut.LogAsync(
            Guid.NewGuid(),
            "CREATE",
            "Goal",
            "goal-id",
            "127.0.0.1",
            "test-agent",
            "{\"key\":\"value\"}",
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Action.Should().Be("CREATE");
        captured.Entity.Should().Be("Goal");
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
