namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// Tipo do contrato de empréstimo/financiamento. Persistido como string
/// (HasConversion&lt;string&gt;) — a apresentação em português fica na camada de UI.
/// </summary>
public enum LoanContractType
{
    Mortgage = 1,
    VehicleFinancing = 2,
    PersonalLoan = 3,
    PayrollLoan = 4,
    BusinessLoan = 5,
    Refinancing = 6,
    CreditAgreement = 7,
    Other = 8
}
