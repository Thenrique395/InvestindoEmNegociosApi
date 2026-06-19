using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class UserSessionServiceTests
{
    [Fact]
    public async Task IssueAsync_Should_Create_New_Refresh_Token()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        jwtTokenGenerator
            .Setup(x => x.Generate(user))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var sut = new UserSessionService(refreshTokenRepository.Object, jwtTokenGenerator.Object, Mock.Of<ILogger<UserSessionService>>());

        var result = await sut.IssueAsync(user);

        result.Token.Should().Be("jwt-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReissueAsync_Should_Revoke_Existing_Tokens_And_Create_New_Refresh_Token()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        jwtTokenGenerator
            .Setup(x => x.Generate(user))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var sut = new UserSessionService(refreshTokenRepository.Object, jwtTokenGenerator.Object, Mock.Of<ILogger<UserSessionService>>());

        var result = await sut.ReissueAsync(user, DateTime.UtcNow);

        result.Token.Should().Be("jwt-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        refreshTokenRepository.Verify(x => x.RevokeActiveByUserAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RotateAsync_Should_Revoke_Current_Token_With_Replacement_Hash()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var currentToken = new RefreshToken(user.Id, "stored-hash", DateTime.UtcNow.AddDays(1));
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        jwtTokenGenerator
            .Setup(x => x.Generate(user))
            .Returns(new TokenResult("jwt-token", DateTime.UtcNow.AddHours(1)));

        var sut = new UserSessionService(refreshTokenRepository.Object, jwtTokenGenerator.Object, Mock.Of<ILogger<UserSessionService>>());

        var result = await sut.RotateAsync(user, currentToken, DateTime.UtcNow);

        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        currentToken.IsRevoked.Should().BeTrue();
        currentToken.ReplacedByTokenHash.Should().NotBeNullOrWhiteSpace();
        refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
