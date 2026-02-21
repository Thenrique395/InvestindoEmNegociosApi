using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestindoEmNegocio.Tests;

public class AuthFacadeServiceTests
{
    [Fact]
    public async Task LoginAsync_Should_Log_Audit_On_Success()
    {
        var authResponse = NewAuthResponse();
        var authService = new FakeAuthService
        {
            OnLoginAsync = (_, _) => Task.FromResult(authResponse)
        };
        var auditService = new FakeAuditService();
        var sut = new AuthFacadeService(authService, auditService, NullLogger<AuthFacadeService>.Instance);

        var result = await sut.LoginAsync(new LoginRequest("user@mail.com", "123456"), "127.0.0.1", "xunit");

        Assert.Equal(authResponse.UserId, result.UserId);
        Assert.Single(auditService.LogEntries);
        Assert.Equal("LOGIN", auditService.LogEntries[0].Action);
    }

    [Fact]
    public async Task LoginAsync_Should_Map_UnauthorizedAccessException_To_401()
    {
        var authService = new FakeAuthService
        {
            OnLoginAsync = (_, _) => Task.FromException<AuthResponse>(new UnauthorizedAccessException())
        };
        var sut = new AuthFacadeService(authService, new FakeAuditService(), NullLogger<AuthFacadeService>.Instance);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() =>
            sut.LoginAsync(new LoginRequest("user@mail.com", "wrong"), "127.0.0.1", "xunit"));

        Assert.Equal(StatusCodes.Status401Unauthorized, ex.StatusCode);
        Assert.Equal("Credenciais inválidas", ex.Title);
    }

    [Fact]
    public async Task RegisterAsync_Should_Map_InvalidOperationException_To_409()
    {
        var authService = new FakeAuthService
        {
            OnRegisterAsync = (_, _) => Task.FromException<AuthResponse>(new InvalidOperationException("Email já existe"))
        };
        var sut = new AuthFacadeService(authService, new FakeAuditService(), NullLogger<AuthFacadeService>.Instance);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() =>
            sut.RegisterAsync(new RegisterUserRequest("User", "user@mail.com", "123456")));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
        Assert.Equal("Registro inválido", ex.Title);
    }

    private static AuthResponse NewAuthResponse() =>
        new(Guid.NewGuid(), "User", "user@mail.com", "User", "token", "refresh", DateTime.UtcNow.AddHours(1));

    private sealed class FakeAuthService : IAuthService
    {
        public Func<RegisterUserRequest, CancellationToken, Task<AuthResponse>>? OnRegisterAsync { get; init; }
        public Func<LoginRequest, CancellationToken, Task<AuthResponse>>? OnLoginAsync { get; init; }
        public Func<Guid, ChangePasswordRequest, CancellationToken, Task>? OnChangePasswordAsync { get; init; }
        public Func<RefreshTokenRequest, CancellationToken, Task<AuthResponse>>? OnRefreshAsync { get; init; }
        public Func<RefreshTokenRequest, CancellationToken, Task>? OnLogoutAsync { get; init; }

        public Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            OnRegisterAsync?.Invoke(request, cancellationToken) ?? Task.FromResult(NewAuthResponse());

        public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            OnLoginAsync?.Invoke(request, cancellationToken) ?? Task.FromResult(NewAuthResponse());

        public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
            OnChangePasswordAsync?.Invoke(userId, request, cancellationToken) ?? Task.CompletedTask;

        public Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            OnRefreshAsync?.Invoke(request, cancellationToken) ?? Task.FromResult(NewAuthResponse());

        public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            OnLogoutAsync?.Invoke(request, cancellationToken) ?? Task.CompletedTask;
    }

    private sealed class FakeAuditService : IAuditService
    {
        public List<AuditEntry> LogEntries { get; } = [];

        public Task LogAsync(
            Guid? userId,
            string action,
            string entity,
            string? entityId,
            string? ipAddress,
            string? userAgent,
            string? metadata,
            CancellationToken cancellationToken = default)
        {
            LogEntries.Add(new AuditEntry(userId, action, entity, entityId, ipAddress, userAgent, metadata));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(
        Guid? UserId,
        string Action,
        string Entity,
        string? EntityId,
        string? IpAddress,
        string? UserAgent,
        string? Metadata);
}
