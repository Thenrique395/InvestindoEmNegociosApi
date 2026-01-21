using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record InstallmentResponse(Guid Id, Guid PlanId, int InstallmentNo, DateOnly DueDate, decimal Amount, InstallmentStatus Status);
