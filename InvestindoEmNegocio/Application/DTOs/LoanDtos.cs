using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed record LoanContractRequest(
    string Title,
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int TermMonths,
    LoanAmortizationType AmortizationType,
    DateOnly StartDate,
    int PaymentDay);

public sealed record LoanInstallmentResponse(
    Guid Id,
    int InstallmentNo,
    DateOnly DueDate,
    decimal BeginningBalance,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal EndingBalance,
    LoanInstallmentStatus Status,
    DateTime? PaidAt);

public sealed record LoanContractResponse(
    Guid Id,
    string Title,
    decimal PrincipalAmount,
    decimal AnnualInterestRate,
    int TermMonths,
    LoanAmortizationType AmortizationType,
    DateOnly StartDate,
    int PaymentDay,
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal TotalInterest,
    LoanStatus Status,
    decimal OpenBalance,
    int OpenInstallments,
    DateTime CreatedAt,
    IReadOnlyList<LoanInstallmentResponse> Installments);

public sealed record LoanSimulationResponse(
    decimal MonthlyPayment,
    decimal TotalCost,
    decimal TotalInterest,
    LoanAmortizationType AmortizationType,
    IReadOnlyList<LoanInstallmentResponse> Installments);
