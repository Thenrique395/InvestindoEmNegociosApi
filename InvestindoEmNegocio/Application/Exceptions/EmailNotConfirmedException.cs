namespace InvestindoEmNegocio.Application.Exceptions;

// Lançada no login quando o e-mail do usuário ainda não foi confirmado (double opt-in).
public sealed class EmailNotConfirmedException(string message) : Exception(message);
