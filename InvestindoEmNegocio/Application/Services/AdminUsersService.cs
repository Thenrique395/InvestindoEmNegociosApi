using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminUsersService(IUserRepository userRepository) : IAdminUsersService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        return users
            .Select(u => new UserSummaryResponse(u.Id, u.Name, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToList();
    }

    public async Task<UserSummaryResponse> UpdateRoleAsync(Guid id, string role, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            throw new AppProblemException("Role inválida", "Role informada não é válida.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        user.SetRole(parsedRole);
        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível atualizar o usuário no momento.",
                StatusCodes.Status409Conflict);
        }

        return new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
    }

    public async Task<UserSummaryResponse> UpdateStatusAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (id == currentUserId && !isActive)
        {
            throw new AppProblemException("Ação inválida", "Você não pode bloquear seu próprio acesso.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        if (isActive) user.Activate();
        else user.Deactivate();

        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível atualizar o usuário no momento.",
                StatusCodes.Status409Conflict);
        }

        return new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (id == currentUserId)
        {
            throw new AppProblemException("Ação inválida", "Você não pode excluir seu próprio usuário.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        userRepository.Remove(user);
        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível excluir o usuário no momento.",
                StatusCodes.Status409Conflict);
        }
    }
}
