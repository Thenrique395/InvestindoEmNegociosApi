using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Enums;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class OfxImportServiceTests
{
    [Fact]
    public async Task ExtractAsync_Should_Parse_Transactions()
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

        var sut = BuildSut(engine);

        var result = await sut.ExtractAsync(userId, accountId, BuildStream(SampleOfx), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.All(x => !x.IsDuplicate).Should().BeTrue();
        result.Items.Should().Contain(x => x.Description == "Salario Empresa" && x.Kind == AccountTransactionKind.Credit);
    }

    [Fact]
    public async Task ImportAsync_Should_Create_Only_New_Transactions_When_SkipDuplicates_Is_True()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var engine = new Mock<IBankStatementImportEngine>();
        var request = new BankStatementImportRequest(
            accountId,
            true,
            [
                new BankStatementImportItemDto("2026-03-01T00:00:00Z", 10m, AccountTransactionKind.Debit, "Padaria Centro", "Compra padaria", "FIT-1", "DEBIT", null),
                new BankStatementImportItemDto("2026-03-05T00:00:00Z", 2500m, AccountTransactionKind.Credit, "Salario Empresa", "Credito salario", "FIT-2", "CREDIT", null)
            ]);
        engine
            .Setup(x => x.ImportAsync(userId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankStatementImportResultResponse(1, 1));

        var sut = BuildSut(engine);

        var result = await sut.ImportAsync(userId, request, CancellationToken.None);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(1);
    }

    private static OfxImportService BuildSut(Mock<IBankStatementImportEngine>? engine = null)
        => new(engine?.Object ?? Mock.Of<IBankStatementImportEngine>());

    private static MemoryStream BuildStream(string content) => new(Encoding.UTF8.GetBytes(content));

    private const string SampleOfx = """
OFXHEADER:100
DATA:OFXSGML
VERSION:102

<OFX>
  <BANKMSGSRSV1>
    <STMTTRNRS>
      <STMTRS>
        <BANKACCTFROM>
          <BANKID>260
          <ACCTID>00012345
          <ACCTTYPE>CHECKING
        </BANKACCTFROM>
        <BANKTRANLIST>
          <DTSTART>20260301000000
          <DTEND>20260331235959
          <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20260301103000
            <TRNAMT>-10.00
            <FITID>FIT-1
            <NAME>Padaria Centro
            <MEMO>Compra padaria
          </STMTTRN>
          <STMTTRN>
            <TRNTYPE>CREDIT
            <DTPOSTED>20260305120000
            <TRNAMT>2500.00
            <FITID>FIT-2
            <NAME>Salario Empresa
            <MEMO>Credito salario
          </STMTTRN>
        </BANKTRANLIST>
        <LEDGERBAL>
          <BALAMT>3490.00
        </LEDGERBAL>
      </STMTRS>
    </STMTTRNRS>
  </BANKMSGSRSV1>
</OFX>
""";
}
