namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICurrentSpaceAccessor
{
    /// <summary>
    /// Espaço ativo da requisição autenticada atual, lido da claim do JWT. É null fora de uma
    /// requisição HTTP autenticada (jobs em segundo plano, fluxo de login/registro antes do
    /// token existir) — nesses casos os repositórios não filtram por espaço.
    /// </summary>
    Guid? SpaceId { get; }

    Guid RequireSpaceId();
}
