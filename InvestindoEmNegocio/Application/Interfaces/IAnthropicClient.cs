namespace InvestindoEmNegocio.Application.Interfaces;

/// <summary>
/// Cliente fino da Messages API da Anthropic (Claude) — sem SDK oficial usado de propósito,
/// mesmo princípio do gateway do Mercado Pago (chamada HTTP direta, vocabulário do provedor
/// isolado nesta camada).
/// </summary>
public interface IAnthropicClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
