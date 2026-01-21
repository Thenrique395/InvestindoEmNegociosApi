using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IJwtTokenGenerator
{
    TokenResult Generate(User user);
}

public record TokenResult(string Token, DateTime ExpiresAt);
