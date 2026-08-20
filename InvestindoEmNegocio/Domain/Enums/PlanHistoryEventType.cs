namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// O que aconteceu com o lançamento. Persistido como string: o histórico é lido
/// por humanos e um número no banco não diz nada em consulta manual.
/// </summary>
public enum PlanHistoryEventType
{
    /// <summary>Lançamento criado.</summary>
    Created,

    /// <summary>Valor alterado — guarda o antes e o depois.</summary>
    AmountChanged,

    /// <summary>Categoria definida ou trocada.</summary>
    CategoryChanged,

    /// <summary>Nome/descrição alterado.</summary>
    TitleChanged,

    /// <summary>Vencimento de uma parcela alterado.</summary>
    DueDateChanged,

    /// <summary>Pagamento registrado numa parcela.</summary>
    PaymentRegistered,

    /// <summary>Pagamento estornado.</summary>
    PaymentReversed,

    /// <summary>Parcela antecipada.</summary>
    Anticipated,

    /// <summary>Parcela excluída (sem apagar o lançamento inteiro).</summary>
    InstallmentDeleted,

    /// <summary>
    /// Vencimento passou sem pagamento. Não vem de ação de ninguém: é derivado
    /// da data da parcela no momento da leitura, e por isso nunca é gravado.
    /// </summary>
    DueDatePassed
}
