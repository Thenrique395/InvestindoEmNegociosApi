using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException("E-mail já está em uso.");
        }

        var passwordHash = BCryptNet.HashPassword(request.Password);
        var user = new User(request.Name.Trim(), request.Email.Trim().ToLowerInvariant(), passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.Generate(user);
        return new AuthResponse(user.Id, user.Name, user.Email, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);

        if (user is null || !BCryptNet.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        user.UpdateLastLogin(DateTime.UtcNow);
        // Como o UpdateLastLogin já atualiza UpdatedAt, apenas persistimos
        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.Generate(user);
        return new AuthResponse(user.Id, user.Name, user.Email, token);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new UnauthorizedAccessException("Usuário não encontrado.");

        if (!BCryptNet.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Senha atual inválida.");

        var newHash = BCryptNet.HashPassword(request.NewPassword);
        user.ChangePassword(newHash);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }
}
