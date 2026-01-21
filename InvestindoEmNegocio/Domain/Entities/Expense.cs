namespace InvestindoEmNegocio.Domain.Entities;

public class Expense
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public DateTime Vencimento { get; private set; }
    public Guid? CardId { get; private set; }
    public int? ParcelaNumero { get; private set; }
    public int? ParcelasTotal { get; private set; }
    public string? SerieId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Expense() { }

    public Expense(Guid userId, string nome, string categoria, decimal valor, DateTime vencimento, Guid? cardId = null,
        int? parcelaNumero = null, int? parcelasTotal = null, string? serieId = null)
    {
        UserId = userId;
        Nome = nome.Trim();
        Categoria = categoria.Trim();
        Valor = valor;
        Vencimento = vencimento.Kind == DateTimeKind.Utc ? vencimento : DateTime.SpecifyKind(vencimento, DateTimeKind.Utc);
        CardId = cardId;
        ParcelaNumero = parcelaNumero;
        ParcelasTotal = parcelasTotal;
        SerieId = serieId;
    }
}
