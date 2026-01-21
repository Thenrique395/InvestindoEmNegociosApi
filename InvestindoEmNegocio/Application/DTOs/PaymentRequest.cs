namespace InvestindoEmNegocio.Application.DTOs;

public record PaymentRequest(DateTime PaidAt, decimal PaidAmount, int? MethodId = null, string? Note = null);
