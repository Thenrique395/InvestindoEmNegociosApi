namespace InvestindoEmNegocio.Domain.Entities;

public class Income
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Fonte { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public DateTime Recebimento { get; private set; }
    public bool Fixa { get; private set; }
    public string? FixaInicio { get; private set; }
    public string? FixaFim { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Income() { }

    public Income(Guid userId, string fonte, decimal valor, DateTime recebimento, bool fixa = false, string? fixaInicio = null, string? fixaFim = null)
    {
        UserId = userId;
        Fonte = fonte.Trim();
        Valor = valor;
        Recebimento = recebimento.Kind == DateTimeKind.Utc ? recebimento : DateTime.SpecifyKind(recebimento, DateTimeKind.Utc);
        Fixa = fixa;
        FixaInicio = fixaInicio;
        FixaFim = fixaFim;
    }
}
