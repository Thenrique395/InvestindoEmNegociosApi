namespace InvestindoEmNegocio.Application.Interfaces;

/// <summary>Seleciona explicitamente o espaço durante um job, restaurando o contexto ao terminar.</summary>
public interface ISpaceExecutionScope
{
    IDisposable BeginScope(Guid spaceId);
}
