using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using System.Reflection;

namespace InvestindoEmNegocio.Tests;

public class B3ReportParserTests
{
    [Fact]
    public void Parse_Should_Extract_Header_Reference_And_Sections()
    {
        var raw = """
            JOAO SILVA | CPF/CNPJ: 12345678900
            Data: 02/2026
            Posição - Ações
            PETR4 - PETROBRAS ON BANCO TESTE S/A 10 R$ 30,00 R$ 300,00
            Proventos recebidos
            PETR4 - PETROBRAS 15/02/2026 Dividendo BANCO TESTE S/A 10 R$ 1,50 R$ 15,00
            Resumo dos negócios no período
            PETR4 18/02/2026 BANCO TESTE S/A 2 0 2 R$ 31,00 R$ 0,00
            Relatório mensal consolidado
            """;

        var snapshot = Parse(raw);

        snapshot.ReferenceMonth.Should().Be("02/2026");
        snapshot.HolderName.Should().Be("JOAO SILVA");
        snapshot.Document.Should().Be("12345678900");

        snapshot.Positions.Should().ContainSingle();
        snapshot.Positions[0].Product.Should().Be("PETR4 - PETROBRAS");
        snapshot.Positions[0].Quantity.Should().Be(10m);
        snapshot.Positions[0].ClosingPrice.Should().Be(30m);

        snapshot.Incomes.Should().ContainSingle();
        snapshot.Incomes[0].EventType.Should().Be("Dividendo");
        snapshot.Incomes[0].NetValue.Should().Be(15m);

        snapshot.Trades.Should().ContainSingle();
        snapshot.Trades[0].Code.Should().Be("PETR4");
        snapshot.Trades[0].BuyQuantity.Should().Be(2m);
        snapshot.Trades[0].AvgBuyPrice.Should().Be(31m);
    }

    [Fact]
    public void Parse_Should_Fallback_To_Annual_Income_Format_When_Needed()
    {
        var raw = """
            JOAO SILVA | CPF/CNPJ: 12345678900
            Data: 02/2026
            PETR4 Dividendo R$ 123,45
            """;

        var snapshot = Parse(raw);

        snapshot.Incomes.Should().ContainSingle();
        snapshot.Incomes[0].Product.Should().Be("PETR4");
        snapshot.Incomes[0].EventType.Should().Be("Dividendo");
        snapshot.Incomes[0].NetValue.Should().Be(123.45m);
    }

    [Fact]
    public void Parse_Should_Fallback_To_Compact_Trades_Format_When_Needed()
    {
        var raw = """
            JOAO SILVA | CPF/CNPJ: 12345678900
            Data: 02/2026
            Resumo dos negócios no período
            PETR418/02/2026BANCO TESTE S/A200R$ 31,00R$ 0,00
            Relatório mensal consolidado
            """;

        var snapshot = Parse(raw);

        snapshot.Trades.Should().ContainSingle();
        snapshot.Trades[0].Code.Should().Be("PETR4");
        snapshot.Trades[0].Period.Should().Be("18/02/2026");
        snapshot.Trades[0].BuyQuantity.Should().Be(2m);
        snapshot.Trades[0].SellQuantity.Should().Be(0m);
        snapshot.Trades[0].NetQuantity.Should().Be(0m);
    }

    private static ParsedSnapshotView Parse(string rawText)
    {
        var parserType = typeof(B3ImportService).Assembly.GetType("InvestindoEmNegocio.Application.Services.B3ReportParser")
                         ?? throw new InvalidOperationException("B3ReportParser não encontrado.");

        var parseMethod = parserType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
                          ?? throw new InvalidOperationException("Método Parse não encontrado.");

        var snapshot = parseMethod.Invoke(null, [rawText])
                       ?? throw new InvalidOperationException("Parse retornou nulo.");

        var snapshotType = snapshot.GetType();

        return new ParsedSnapshotView(
            ReferenceMonth: snapshotType.GetProperty("ReferenceMonth")?.GetValue(snapshot) as string,
            HolderName: snapshotType.GetProperty("HolderName")?.GetValue(snapshot) as string,
            Document: snapshotType.GetProperty("Document")?.GetValue(snapshot) as string,
            Positions: (snapshotType.GetProperty("Positions")?.GetValue(snapshot) as List<B3ExtractPosition>) ?? [],
            Incomes: (snapshotType.GetProperty("Incomes")?.GetValue(snapshot) as List<B3ExtractIncome>) ?? [],
            Trades: (snapshotType.GetProperty("Trades")?.GetValue(snapshot) as List<B3ExtractTrade>) ?? []);
    }

    private sealed record ParsedSnapshotView(
        string? ReferenceMonth,
        string? HolderName,
        string? Document,
        List<B3ExtractPosition> Positions,
        List<B3ExtractIncome> Incomes,
        List<B3ExtractTrade> Trades);
}
