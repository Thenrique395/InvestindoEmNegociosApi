using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class AccountTransferService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    ILogger<AccountTransferService> logger) : IAccountTransferService
{
    private readonly ILogger<AccountTransferService> _logger = logger;

    public async Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Valor da transferência deve ser maior que zero.");
        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Conta de origem e destino devem ser diferentes.");

        var from = await accountRepository.GetByIdAsync(request.FromAccountId, userId, cancellationToken);
        var to = await accountRepository.GetByIdAsync(request.ToAccountId, userId, cancellationToken);
        if (from is null || to is null) return null;
        if (!from.IsActive) throw new ArgumentException("Conta de origem está inativa.");
        if (!to.IsActive) throw new ArgumentException("Conta de destino está inativa.");

        var occurredAt = (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime();
        var transferId = Guid.NewGuid();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transferência {from.Name} -> {to.Name}"
            : request.Description.Trim();

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            from.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Debit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            to.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Credit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account transfer created {UserId} {TransferId} {FromAccountId} -> {ToAccountId} Amount {Amount}",
            userId,
            transferId,
            from.Id,
            to.Id,
            request.Amount);

        return new AccountTransferResponse(
            transferId,
            from.Id,
            to.Id,
            request.Amount,
            occurredAt,
            description);
    }
}
