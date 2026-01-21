namespace InvestindoEmNegocio.Application.DTOs;

public record GoalContributionRequest(decimal Amount, DateOnly Date, string? Note);

public record GoalContributionResponse(Guid Id, decimal Amount, DateOnly Date, string? Note, DateTime CreatedAt);
