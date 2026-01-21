namespace InvestindoEmNegocio.Application.DTOs;

public record AnticipationRequest(DateOnly DueDate, string? Note = null);
