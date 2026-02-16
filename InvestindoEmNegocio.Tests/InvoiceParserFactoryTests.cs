using System.Text;
using System.Text.RegularExpressions;
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
        Assert.NotEmpty(response.Items);

        var alice = response.Items.FirstOrDefault(i => i.Description.Contains("ALICESANTOS", StringComparison.OrdinalIgnoreCase));
        Assert.True(alice is not null, DumpItems(response.Items));
        Assert.Equal("02/12", alice!.Date);
        Assert.Equal("R$ 118,32", alice.Amount);
        Assert.True(alice.IsInstallment, DumpItems(response.Items));
        Assert.Equal(2, alice.InstallmentCurrent);
        Assert.Equal(4, alice.InstallmentTotal);

        var shopee = response.Items.FirstOrDefault(i => i.Description.Contains("BENECASALOJAOF", StringComparison.OrdinalIgnoreCase));
        Assert.True(shopee is not null, DumpItems(response.Items));
        Assert.Equal("R$ 60,61", shopee!.Amount);
        Assert.True(shopee.IsInstallment, DumpItems(response.Items));
        Assert.Equal(2, shopee.InstallmentCurrent);
        Assert.Equal(3, shopee.InstallmentTotal);
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

        Assert.DoesNotContain(response.Items, i => i.Description.Contains("PAGAMENTO EFETUADO", StringComparison.OrdinalIgnoreCase));
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
