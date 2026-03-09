using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Enums;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class CsvImportServiceTests
{
    [Fact]
    public async Task ExtractAsync_Should_Parse_Semicolon_Csv_With_Header()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var engine = new Mock<IBankStatementImportEngine>();
        engine
            .Setup(x => x.BuildPreviewAsync(userId, accountId, It.IsAny<IReadOnlyList<BankStatementImportItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid? _, IReadOnlyList<BankStatementImportItemDto> items, CancellationToken _) =>
                items.Select(item => new BankStatementPreviewItemDto(
                    item.PostedAt,
                    item.Amount,
                    item.Kind,
                    item.Description,
                    item.Memo,
                    item.ExternalId,
                    item.Type,
                    false,
                    null,
                    null)).ToList());

        var sut = new CsvImportService(engine.Object);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
data;descricao;valor;tipo
01/03/2026;Padaria;-10,50;debit
05/03/2026;Salario;2500,00;credit
"""));

        var result = await sut.ExtractAsync(userId, accountId, stream, CancellationToken.None);

        result.Delimiter.Should().Be(";");
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(x => x.Description == "Padaria" && x.Kind == AccountTransactionKind.Debit);
        result.Items.Should().Contain(x => x.Description == "Salario" && x.Kind == AccountTransactionKind.Credit);
    }
}
