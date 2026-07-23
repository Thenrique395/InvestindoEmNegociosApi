using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class UserAccountBootstrapService(
    IAccountRepository accountRepository,
    ILogger<UserAccountBootstrapService> logger) : IUserAccountBootstrapService
{
    public async Task EnsureDefaultAccountForBasicAsync(User user, Guid spaceId, CancellationToken cancellationToken = default)
    {
        if (user.Role != UserRole.Basic)
            return;

        // Consulta pelo espaço EXPLÍCITO (recebido por parâmetro), não pelo espaço
        // ambiente: durante o login, se a requisição trouxe um cookie de sessão de
        // OUTRO usuário, o espaço ambiente estaria errado — levando a achar "0 contas"
        // e tentar inserir uma "Conta principal" duplicada (viola índice único → 500).
        var accounts = await accountRepository.ListByUserAndSpaceAsync(user.Id, spaceId, cancellationToken) ?? [];
        if (accounts.Count == 0)
        {
            var account = new Account(user.Id, spaceId, "Conta principal", AccountType.Checking, 0m);
            await accountRepository.AddAsync(account, cancellationToken);
            await accountRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default account created for basic user {UserId}", user.Id);
            return;
        }

        var firstActive = accounts.FirstOrDefault(a => a.IsActive);
        if (firstActive is not null)
            return;

        accounts[0].Activate();
        await accountRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Default account reactivated for basic user {UserId}", user.Id);
    }
}
