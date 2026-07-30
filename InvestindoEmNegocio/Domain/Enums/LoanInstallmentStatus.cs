namespace InvestindoEmNegocio.Domain.Enums;

public enum LoanInstallmentStatus
{
    // Valores atuais preservados (persistidos como string via HasConversion<string>).
    Open = 1,
    Paid = 2,
    // Novos status (aditivos).
    Overdue = 3,
    PartiallyPaid = 4,
    Anticipated = 5,
    Cancelled = 6,
    Renegotiated = 7
}
