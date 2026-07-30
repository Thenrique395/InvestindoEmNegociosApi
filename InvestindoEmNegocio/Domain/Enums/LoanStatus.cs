namespace InvestindoEmNegocio.Domain.Enums;

public enum LoanStatus
{
    // Valores atuais preservados (persistidos como string via HasConversion<string>).
    Active = 1,
    Closed = 2,
    // Novos status (aditivos).
    Draft = 3,
    Overdue = 4,
    Cancelled = 5,
    Archived = 6,
    Renegotiated = 7
}
