using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_Should_Throw_When_Email_Already_Exists()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.EmailExistsAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildSut(userRepository: userRepository);

        Func<Task> act = async () => await sut.RegisterAsync(new RegisterUserRequest("User", "user@local", "Password123!"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*E-mail já está em uso*");
    }

    [Fact]
    public async Task LoginAsync_Should_Throw_When_Password_Is_Invalid_And_Persist_Attempt()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var user = new User("User", "user@local", passwordHash);

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        Func<Task> act = async () => await sut.LoginAsync(new LoginRequest("user@local", "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Credenciais inválidas*");
        user.FailedLoginAttempts.Should().Be(1);
        userRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_Should_Revoke_RefreshToken_When_Valid()
    {
        var refreshToken = new RefreshToken(Guid.NewGuid(), "hashed-token", DateTime.UtcNow.AddDays(1));
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        var sut = BuildSut(refreshTokenRepository: refreshTokenRepository);

        await sut.LogoutAsync(new RefreshTokenRequest("plain-token"), CancellationToken.None);

        refreshToken.IsRevoked.Should().BeTrue();
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_Should_Rotate_RefreshToken_When_Valid()
    {
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        var storedRefreshToken = new RefreshToken(user.Id, "stored-hash", DateTime.UtcNow.AddDays(1));
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRefreshToken);

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var jwtTokenGenerator = new Mock<IJwtTokenGenerator>();
        jwtTokenGenerator
            .Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("new-access-token", DateTime.UtcNow.AddMinutes(30)));

        var sut = BuildSut(userRepository, refreshTokenRepository, jwtTokenGenerator);

        var result = await sut.RefreshAsync(new RefreshTokenRequest("plain-refresh-token"), CancellationToken.None);

        result.Token.Should().Be("new-access-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        storedRefreshToken.IsRevoked.Should().BeTrue();
        storedRefreshToken.ReplacedByTokenHash.Should().NotBeNullOrWhiteSpace();
        refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshAsync_Should_Throw_When_Token_Is_Invalid()
    {
        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var sut = BuildSut(refreshTokenRepository: refreshTokenRepository);

        Func<Task> act = async () => await sut.RefreshAsync(new RefreshTokenRequest("invalid"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Refresh token inválido*");
        refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_Should_Lock_User_After_Max_Failed_Attempts()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var user = new User("User", "user@local", passwordHash);

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var act = async () => await sut.LoginAsync(new LoginRequest("user@local", "wrong"), CancellationToken.None);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        Func<Task> lockedAttempt = async () => await sut.LoginAsync(new LoginRequest("user@local", "wrong"), CancellationToken.None);
        await lockedAttempt.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Conta bloqueada temporariamente*");

        user.IsLocked(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_Should_Throw_When_CurrentPassword_Is_Invalid()
    {
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        Func<Task> act = async () => await sut.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest("wrong-current", "NewPassword123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Senha atual inválida*");
    }

    [Fact]
    public async Task ChangePasswordAsync_Should_Update_Hash_When_CurrentPassword_Is_Valid()
    {
        var oldPassword = "Password123!";
        var newPassword = "NewPassword123!";
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword(oldPassword));
        var previousHash = user.PasswordHash;

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        await sut.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest(oldPassword, newPassword),
            CancellationToken.None);

        user.PasswordHash.Should().NotBe(previousHash);
        BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash).Should().BeTrue();
        userRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AuthService BuildSut(
        Mock<IUserRepository>? userRepository = null,
        Mock<IRefreshTokenRepository>? refreshTokenRepository = null,
        Mock<IJwtTokenGenerator>? jwtTokenGenerator = null)
    {
        return new AuthService(
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            refreshTokenRepository?.Object ?? Mock.Of<IRefreshTokenRepository>(),
            jwtTokenGenerator?.Object ?? CreateDefaultTokenGenerator().Object,
            NullLogger<AuthService>.Instance);
    }

    private static Mock<IJwtTokenGenerator> CreateDefaultTokenGenerator()
    {
        var jwt = new Mock<IJwtTokenGenerator>();
        jwt
            .Setup(x => x.Generate(It.IsAny<User>()))
            .Returns(new TokenResult("access-token", DateTime.UtcNow.AddHours(1)));
        return jwt;
    }
}
