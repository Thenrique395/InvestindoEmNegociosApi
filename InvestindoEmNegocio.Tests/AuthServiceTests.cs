using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

        Func<Task> act = async () => await sut.RegisterAsync(new RegisterUserRequest("User", "user@local", "Password123!", "52998224725"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*E-mail já está em uso*");
    }

    [Fact]
    public async Task RegisterAsync_Should_Create_Default_Account_For_Basic_User_When_Base_Is_Empty()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.EmailExistsAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository
            .Setup(x => x.ListByUserAndSpaceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = BuildSut(userRepository: userRepository, accountRepository: accountRepository);

        var response = await sut.RegisterAsync(
            new RegisterUserRequest("User", "user@local", "Password123!", "52998224725"),
            CancellationToken.None);

        response.RequiresEmailConfirmation.Should().BeTrue();
        accountRepository.Verify(
            x => x.AddAsync(
                It.Is<Account>(a => a.UserId == response.UserId && a.Name == "Conta principal" && a.IsActive),
                It.IsAny<CancellationToken>()),
            Times.Once);
        accountRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task LoginAsync_Should_Throw_When_Account_Is_Deactivated()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var user = new User("User", "user@local", passwordHash);
        user.Deactivate();

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        Func<Task> act = async () => await sut.LoginAsync(new LoginRequest("user@local", "Password123!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Credenciais inválidas*");
    }

    [Fact]
    public async Task LoginAsync_Should_Throw_EmailNotConfirmed_When_Email_Not_Confirmed()
    {
        // Senha correta e conta ativa, mas e-mail ainda não confirmado (double opt-in) → bloqueia.
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var user = new User("User", "user@local", passwordHash); // EmailConfirmed = false por padrão

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository);

        Func<Task> act = async () => await sut.LoginAsync(new LoginRequest("user@local", "Password123!"), CancellationToken.None);

        await act.Should().ThrowAsync<Application.Exceptions.EmailNotConfirmedException>();
    }

    [Fact]
    public async Task RefreshAsync_Should_Throw_When_Account_Is_Deactivated()
    {
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        user.Deactivate();
        var stored = new RefreshToken(user.Id, Guid.NewGuid(), "hashed-token", DateTime.UtcNow.AddDays(1));

        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = BuildSut(userRepository: userRepository, refreshTokenRepository: refreshTokenRepository);

        Func<Task> act = async () => await sut.RefreshAsync(new RefreshTokenRequest("plain-token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Refresh token inválido*");
    }

    [Fact]
    public async Task LogoutAsync_Should_Revoke_RefreshToken_When_Valid()
    {
        var refreshToken = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hashed-token", DateTime.UtcNow.AddDays(1));
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
        var storedRefreshToken = new RefreshToken(user.Id, Guid.NewGuid(), "stored-hash", DateTime.UtcNow.AddDays(1));
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
            .Setup(x => x.Generate(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(new TokenResult("new-access-token", DateTime.UtcNow.AddMinutes(30)));

        var sut = BuildSut(
            userRepository: userRepository,
            refreshTokenRepository: refreshTokenRepository,
            jwtTokenGenerator: jwtTokenGenerator);

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

    [Fact]
    public async Task ChangePasswordAsync_Should_Revoke_Sessions_So_Old_Tokens_Stop_Working()
    {
        var oldPassword = "Password123!";
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword(oldPassword));
        var previousTokenVersion = user.TokenVersion;

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        var sut = BuildSut(userRepository: userRepository, refreshTokenRepository: refreshTokenRepository);

        await sut.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest(oldPassword, "NewPassword123!"),
            CancellationToken.None);

        user.TokenVersion.Should().BeGreaterThan(previousTokenVersion);
        refreshTokenRepository.Verify(x => x.RevokeActiveByUserAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_Should_Not_Throw_When_User_Does_Not_Exist()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByEmailAsync("missing@local", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var emailSender = new Mock<IEmailSender>();

        var sut = BuildSut(userRepository: userRepository, emailSender: emailSender);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequest("missing@local"), CancellationToken.None);

        emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_Should_Mark_Token_Used_And_Revoke_Refresh_Tokens()
    {
        var user = new User("User", "user@local", BCrypt.Net.BCrypt.HashPassword("Password123!"));
        var rawResetToken = "plain-reset-token";
        var tokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawResetToken)));
        var resetToken = new PasswordResetToken(user.Id, tokenHash, DateTime.UtcNow.AddMinutes(30));

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var passwordResetRepository = new Mock<IPasswordResetTokenRepository>();
        passwordResetRepository.Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(resetToken);

        var refreshRepository = new Mock<IRefreshTokenRepository>();

        var sut = BuildSut(
            userRepository: userRepository,
            refreshTokenRepository: refreshRepository,
            passwordResetTokenRepository: passwordResetRepository);

        var previousTokenVersion = user.TokenVersion;

        await sut.ResetPasswordAsync(new ResetPasswordRequest(rawResetToken, "NovaSenha123!"), CancellationToken.None);

        resetToken.IsUsed.Should().BeTrue();
        user.TokenVersion.Should().BeGreaterThan(previousTokenVersion);
        refreshRepository.Verify(x => x.RevokeActiveByUserAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        refreshRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IAuthService BuildSut(
        Mock<IUserRepository>? userRepository = null,
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IRefreshTokenRepository>? refreshTokenRepository = null,
        Mock<IPasswordResetTokenRepository>? passwordResetTokenRepository = null,
        Mock<IJwtTokenGenerator>? jwtTokenGenerator = null,
        Mock<IEmailSender>? emailSender = null)
    {
        var spaceBootstrapService = Mock.Of<ISpaceBootstrapService>(x => x.EnsureDefaultSpaceAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()) == Task.FromResult(Guid.NewGuid()));
        var sessionService = new UserSessionService(
            refreshTokenRepository?.Object ?? Mock.Of<IRefreshTokenRepository>(),
            spaceBootstrapService,
            jwtTokenGenerator?.Object ?? CreateDefaultTokenGenerator().Object,
            NullLogger<UserSessionService>.Instance);
        var bootstrapService = new UserAccountBootstrapService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            NullLogger<UserAccountBootstrapService>.Instance);
        var passwordResetService = new PasswordResetService(
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            passwordResetTokenRepository?.Object ?? Mock.Of<IPasswordResetTokenRepository>(),
            sessionService,
            emailSender?.Object ?? Mock.Of<IEmailSender>(),
            Options.Create(new PasswordResetOptions
            {
                FrontendResetUrl = "http://localhost:4200/reset-password",
                TokenExpiryMinutes = 30
            }),
            NullLogger<PasswordResetService>.Instance);

        var authRegistrationService = new AuthRegistrationService(
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            bootstrapService,
            spaceBootstrapService,
            Mock.Of<IEmailConfirmationService>(),
            NullLogger<AuthRegistrationService>.Instance);
        var authAccessService = new AuthAccessService(
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            bootstrapService,
            spaceBootstrapService,
            sessionService,
            NullLogger<AuthAccessService>.Instance);
        var authPasswordService = new AuthPasswordService(
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            passwordResetService,
            sessionService,
            NullLogger<AuthPasswordService>.Instance);

        return new AuthService(
            authRegistrationService,
            authAccessService,
            authPasswordService);
    }

    private static Mock<IJwtTokenGenerator> CreateDefaultTokenGenerator()
    {
        var jwt = new Mock<IJwtTokenGenerator>();
        jwt
            .Setup(x => x.Generate(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(new TokenResult("access-token", DateTime.UtcNow.AddHours(1)));
        return jwt;
    }
}
