using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class UserAccountBootstrapService(
    IAccountRepository accountRepository,
    ILogger<UserAccountBootstrapService> logger) : IUserAccountBootstrapService
{
    public async Task EnsureDefaultAccountForBasicAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user.Role != UserRole.Basic)
            return;

        var accounts = await accountRepository.ListByUserAsync(user.Id, cancellationToken) ?? [];
        if (accounts.Count == 0)
        {
            var account = new Account(user.Id, "Conta principal", AccountType.Checking, 0m);
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
