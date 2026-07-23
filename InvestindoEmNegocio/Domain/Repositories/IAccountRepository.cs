using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IAccountRepository
{
    Task<List<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as contas do usuário em um espaço EXPLÍCITO, sem depender do espaço
    /// ambiente (<c>ICurrentSpaceAccessor</c>). Usado no bootstrap de login/registro,
    /// onde o <c>HttpContext.User</c> pode refletir uma sessão remanescente de OUTRO
    /// usuário — o que levaria o filtro de espaço ambiente ao espaço errado.
    /// Retorna entidades rastreadas (permite ativar a conta e salvar).
    /// </summary>
    Task<List<Account>> ListByUserAndSpaceAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid userId, string name, Guid? ignoreId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    void Remove(Account account);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
