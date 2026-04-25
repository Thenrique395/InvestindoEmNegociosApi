using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AuthApplicationServicesTests
{
    [Fact]
    public async Task LoginAsync_Should_Log_Audit_On_Success()
    {
        var response = NewAuthResponse();
        var authService = new Mock<IAuthService>();
        var auditService = new Mock<IAuditService>();
        authService
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var sut = new AuthAccessApplicationService(authService.Object, auditService.Object, NullLogger<AuthAccessApplicationService>.Instance);

        var result = await sut.LoginAsync(new LoginRequest("user@mail.com", "123456"), "127.0.0.1", "xunit");

        result.Should().BeEquivalentTo(response);
        auditService.Verify(x => x.LogAsync(
                response.UserId,
                "LOGIN",
                "User",
                response.UserId.ToString(),
                "127.0.0.1",
                "xunit",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_Should_Map_UnauthorizedAccessException_To_401()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var sut = new AuthAccessApplicationService(authService.Object, Mock.Of<IAuditService>(), NullLogger<AuthAccessApplicationService>.Instance);

        Func<Task> act = async () => await sut.LoginAsync(new LoginRequest("user@mail.com", "wrong"), "127.0.0.1", "xunit");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        ex.Which.Title.Should().Be("Credenciais inválidas");
    }

    [Fact]
    public async Task RegisterAsync_Should_Map_InvalidOperationException_To_409()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterUserRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email já existe"));
        var sut = new AuthRegistrationApplicationService(authService.Object, NullLogger<AuthRegistrationApplicationService>.Instance);

        Func<Task> act = async () => await sut.RegisterAsync(new RegisterUserRequest("User", "user@mail.com", "123456"));

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        ex.Which.Title.Should().Be("Cadastro inválido");
    }

    [Fact]
    public async Task ChangePasswordAsync_Should_Map_UnauthorizedAccessException_To_401()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(x => x.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Senha atual inválida"));
        var sut = new AuthPasswordApplicationService(authService.Object, Mock.Of<IAuditService>(), NullLogger<AuthPasswordApplicationService>.Instance);

        Func<Task> act = async () => await sut.ChangePasswordAsync(Guid.NewGuid(), new ChangePasswordRequest("old", "new"), "127.0.0.1", "xunit");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        ex.Which.Title.Should().Be("Senha inválida");
    }

    private static AuthResponse NewAuthResponse() =>
        new(Guid.NewGuid(), "User", "user@mail.com", "User", "token", "refresh", DateTime.UtcNow.AddHours(1));
}
