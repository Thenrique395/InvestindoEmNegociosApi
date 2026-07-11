namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>Periodicidade da meta. None = período único.</summary>
public enum RecurrenceType
{
    None,
    Weekly,
    Monthly,
    Quarterly,
    Semiannual,
    Annual,
    Custom
}
