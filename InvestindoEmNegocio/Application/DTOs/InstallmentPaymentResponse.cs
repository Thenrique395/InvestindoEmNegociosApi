namespace InvestindoEmNegocio.Application.DTOs;

public record InstallmentPaymentResponse(
    Guid Id,
    DateTime PaidAt,
    decimal PaidAmount,
    int? MethodId,
    string? Note,
    bool IsReversal,
    bool CanReverse);
