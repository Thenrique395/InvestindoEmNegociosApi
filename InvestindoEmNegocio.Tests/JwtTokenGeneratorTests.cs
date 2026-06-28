using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_Should_Include_TokenVersion_Claim_Matching_User()
    {
        var user = new User("Teste", "user@local", "hash");
        // Simula um usuário cujas sessões já foram revogadas alguma vez (TokenVersion > 0).
        user.RevokeSessions();
        user.RevokeSessions();

        var sut = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Issuer = "InvestindoEmNegocio",
            Audience = "InvestindoEmNegocio",
            SecretKey = "test-secret-key-pelo-menos-32-caracteres-1234567890",
            ExpiresMinutes = 15
        }));

        var spaceId = Guid.NewGuid();
        var result = sut.Generate(user, spaceId);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        var tokenVersionClaim = token.Claims.FirstOrDefault(c => c.Type == JwtTokenGenerator.TokenVersionClaim);
        var spaceIdClaim = token.Claims.FirstOrDefault(c => c.Type == JwtTokenGenerator.SpaceIdClaim);

        tokenVersionClaim.Should().NotBeNull();
        tokenVersionClaim!.Value.Should().Be(user.TokenVersion.ToString());
        spaceIdClaim.Should().NotBeNull();
        spaceIdClaim!.Value.Should().Be(spaceId.ToString());
        user.TokenVersion.Should().Be(2);
    }
}
