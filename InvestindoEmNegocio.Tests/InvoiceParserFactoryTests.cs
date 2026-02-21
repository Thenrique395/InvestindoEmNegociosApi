using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;

namespace InvestindoEmNegocio.Tests;

public class InvoiceParserFactoryTests
{
    [Fact]
    public void Santander_DenseInstallmentValues_ShouldParseCorrectly()
    {
        var raw = """
            Olá, Tiago! Esta é a fatura do seu cartão SANTANDERUNIQUE MASTERCARD.
            Detalhamento da Fatura
            ParcelamentosCompraDataDescriçãoParcelaR$US$202/12MP *ALICESANTOS02/04118,32 02/12SHOPEE *BENECASALOJAOF02/0360,61
            """;

        var response = Parse(raw);
        response.Items.Should().NotBeEmpty();

        var alice = response.Items.FirstOrDefault(i => i.Description.Contains("ALICESANTOS", StringComparison.OrdinalIgnoreCase));
        alice.Should().NotBeNull(DumpItems(response.Items));
        alice!.Date.Should().Be("02/12");
        alice.Amount.Should().Be("R$ 118,32");
        alice.IsInstallment.Should().BeTrue(DumpItems(response.Items));
        alice.InstallmentCurrent.Should().Be(2);
        alice.InstallmentTotal.Should().Be(4);

        var shopee = response.Items.FirstOrDefault(i => i.Description.Contains("BENECASALOJAOF", StringComparison.OrdinalIgnoreCase));
        shopee.Should().NotBeNull(DumpItems(response.Items));
        shopee!.Amount.Should().Be("R$ 60,61");
        shopee.IsInstallment.Should().BeTrue(DumpItems(response.Items));
        shopee.InstallmentCurrent.Should().Be(2);
        shopee.InstallmentTotal.Should().Be(3);
    }

    [Fact]
    public void Itau_ShouldFilterOutPaymentRows()
    {
        var raw = """
            A544501605BPC -00 TIAGO HENRIQUE DOS SANTOS Resumo da fatura em R$
            Lançamentos: compras e saques
            02/0158032775Jamesson  13/18255,50 ALIMENTAÇÃO .RECIFE
            02/12MP *ALICESANTOS02/04118,32 ALIMENTAÇÃO .RECIFE
            02/12SHOPEE *BENECASALOJAOF02/0360,61 ALIMENTAÇÃO .RECIFE
            30/12PAGAMENTO EFETUADO 6167- 4.437,25 DIVERSOS .RECIFE
            """;

        var response = Parse(raw);

        response.Items.Should().NotContain(i =>
            i.Description.Contains("PAGAMENTO EFETUADO", StringComparison.OrdinalIgnoreCase));
    }

    private static InvoiceExtractResponse Parse(string raw)
    {
        var lines = raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => Regex.Replace(l.Trim(), "\\s+", " "))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return new InvoiceParserFactory().Parse(raw, lines);
    }

    private static string DumpItems(IReadOnlyList<InvoiceItemDto> items)
    {
        var sb = new StringBuilder();
        foreach (var i in items)
        {
            sb.AppendLine($"{i.Date} | {i.Description} | {i.Amount} | inst={i.IsInstallment} {i.InstallmentCurrent}/{i.InstallmentTotal}");
        }
        return sb.ToString();
    }
}
