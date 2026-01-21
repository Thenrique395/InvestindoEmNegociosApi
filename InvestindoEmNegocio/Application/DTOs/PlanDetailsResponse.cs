namespace InvestindoEmNegocio.Application.DTOs;

public record PlanDetailsResponse(PlanResponse Plan, IReadOnlyList<InstallmentResponse> Installments);
