namespace InvestindoEmNegocio.Application.DTOs;

public record AuthSessionResponse(Guid UserId, string Name, string Email, string Role, DateTime ExpiresAt);
