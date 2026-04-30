using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class AccountQueryService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository) : IAccountQueryService
{
    public async Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        var responses = new List<AccountResponse>(accounts.Count);

        foreach (var account in accounts)
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            responses.Add(AccountServiceMappings.MapToResponse(account, net));
        }

        return responses;
    }

    public async Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(accountId, userId, cancellationToken);
        return new AccountBalanceResponse(accountId, account.InitialBalance, net, account.InitialBalance + net);
    }
}
