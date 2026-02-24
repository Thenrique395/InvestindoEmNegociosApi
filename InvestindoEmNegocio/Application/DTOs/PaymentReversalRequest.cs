namespace InvestindoEmNegocio.Application.DTOs;

public record PaymentReversalRequest(DateTime? ReversedAt = null, string? Note = null);
