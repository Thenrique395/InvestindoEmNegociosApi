namespace InvestindoEmNegocio.Application.DTOs;

// Resposta do cadastro. Com a confirmação de e-mail (double opt-in), o registro NÃO loga o usuário
// — ele precisa confirmar o e-mail antes. Por isso a resposta não traz tokens/sessão.
public record RegisteredUserResponse(Guid UserId, string Name, string Email, bool RequiresEmailConfirmation);

public record ConfirmEmailRequest(string Token);

public record ResendConfirmationRequest(string Email);
