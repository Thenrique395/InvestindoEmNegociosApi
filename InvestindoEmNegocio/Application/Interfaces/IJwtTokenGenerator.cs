using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string Generate(User user);
}
