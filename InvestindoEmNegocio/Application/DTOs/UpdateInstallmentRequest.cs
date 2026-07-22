namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>
/// Edição de uma única parcela: valor e vencimento. Nome/categoria são do plano
/// e alterados via edição da série inteira.
/// </summary>
public record UpdateInstallmentRequest(decimal Amount, DateOnly DueDate);
