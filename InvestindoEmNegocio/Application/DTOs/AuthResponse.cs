namespace InvestindoEmNegocio.Application.DTOs;

public record AuthResponse(Guid UserId, string Name, string Email, string Role, string Token, string RefreshToken, DateTime ExpiresAt);
