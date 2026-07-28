using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IEmailConfirmationService
{
    // Gera um token, grava e envia o e-mail de confirmação para o usuário.
    Task SendConfirmationAsync(User user, CancellationToken cancellationToken = default);

    // Valida o token bruto e marca o e-mail do usuário como confirmado.
    Task ConfirmAsync(string token, CancellationToken cancellationToken = default);

    // Reenvia o e-mail de confirmação (silencioso; só age se o usuário existir e não estiver confirmado).
    Task ResendAsync(string email, CancellationToken cancellationToken = default);
}
