namespace InvestindoEmNegocio.Application.DTOs;

public record UpdateUserRoleRequest(string Role);

public record UserSummaryResponse(Guid Id, string Name, string Email, string Role, bool IsActive, DateTime CreatedAt);
