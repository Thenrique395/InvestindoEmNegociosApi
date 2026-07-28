using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class EmailConfirmationServiceTests
{
    private static EmailConfirmationService BuildSut(
        Mock<IUserRepository> userRepo,
        Mock<IEmailConfirmationTokenRepository> tokenRepo,
        Mock<IEmailSender>? emailSender = null)
        => new(
            userRepo.Object,
            tokenRepo.Object,
            (emailSender ?? new Mock<IEmailSender>()).Object,
            Options.Create(new EmailConfirmationOptions
            {
                FrontendConfirmUrl = "http://localhost:4200/confirmar-email",
                TokenExpiryMinutes = 1440
            }),
            NullLogger<EmailConfirmationService>.Instance);

    // O service guarda o HASH SHA-256 do token bruto; replicamos aqui para casar o lookup.
    private static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [Fact]
    public async Task ConfirmAsync_Should_Confirm_User_And_Mark_Token_Used_For_Valid_Token()
    {
        const string raw = "raw-token-abc";
        var user = new User("User", "user@local", "hash");
        user.EmailConfirmed.Should().BeFalse();
        var token = new EmailConfirmationToken(user.Id, Hash(raw), DateTime.UtcNow.AddHours(1));

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var tokenRepo = new Mock<IEmailConfirmationTokenRepository>();
        tokenRepo.Setup(x => x.GetByTokenHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var sut = BuildSut(userRepo, tokenRepo);
        await sut.ConfirmAsync(raw);

        user.EmailConfirmed.Should().BeTrue();
        token.IsUsed.Should().BeTrue();
        userRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Throw_For_Expired_Token()
    {
        const string raw = "raw-token-exp";
        var token = new EmailConfirmationToken(Guid.NewGuid(), Hash(raw), DateTime.UtcNow.AddHours(-1));
        var tokenRepo = new Mock<IEmailConfirmationTokenRepository>();
        tokenRepo.Setup(x => x.GetByTokenHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var sut = BuildSut(new Mock<IUserRepository>(), tokenRepo);

        Func<Task> act = () => sut.ConfirmAsync(raw);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmAsync_Should_Throw_For_Unknown_Token()
    {
        var tokenRepo = new Mock<IEmailConfirmationTokenRepository>();
        tokenRepo.Setup(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailConfirmationToken?)null);

        var sut = BuildSut(new Mock<IUserRepository>(), tokenRepo);

        Func<Task> act = () => sut.ConfirmAsync("qualquer-coisa");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ResendAsync_Should_Send_When_User_Exists_And_Not_Confirmed()
    {
        var user = new User("User", "user@local", "hash"); // não confirmado
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var tokenRepo = new Mock<IEmailConfirmationTokenRepository>();
        var emailSender = new Mock<IEmailSender>();

        var sut = BuildSut(userRepo, tokenRepo, emailSender);
        await sut.ResendAsync("user@local");

        tokenRepo.Verify(x => x.AddAsync(It.IsAny<EmailConfirmationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(x => x.SendAsync("user@local", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendAsync_Should_Do_Nothing_When_User_Already_Confirmed()
    {
        var user = new User("User", "user@local", "hash");
        user.ConfirmEmail();
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetByEmailAsync("user@local", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var tokenRepo = new Mock<IEmailConfirmationTokenRepository>();

        var sut = BuildSut(userRepo, tokenRepo);
        await sut.ResendAsync("user@local");

        tokenRepo.Verify(x => x.AddAsync(It.IsAny<EmailConfirmationToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
