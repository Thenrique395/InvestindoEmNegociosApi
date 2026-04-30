using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class AccountCommandService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ILogger<AccountCommandService> logger) : IAccountCommandService
{
    private readonly ILogger<AccountCommandService> _logger = logger;

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        if (await accountRepository.ExistsByNameAsync(userId, request.Name, null, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        var account = new Account(userId, request.Name, request.Type, request.InitialBalance);
        if (!request.IsActive) account.Deactivate();

        await accountRepository.AddAsync(account, cancellationToken);
        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account created {UserId} {AccountId}", userId, account.Id);
        return AccountServiceMappings.MapToResponse(account, 0m);
    }

    public async Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        if (await accountRepository.ExistsByNameAsync(userId, request.Name, accountId, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        account.Update(request.Name, request.Type, request.InitialBalance);
        if (request.IsActive) account.Activate();
        else account.Deactivate();

        await accountRepository.SaveChangesAsync(cancellationToken);

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
        _logger.LogInformation("Account updated {UserId} {AccountId}", userId, account.Id);
        return AccountServiceMappings.MapToResponse(account, net);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return false;

        accountRepository.Remove(account);
        await accountRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Account deleted {UserId} {AccountId}", userId, accountId);
        return true;
    }
}
