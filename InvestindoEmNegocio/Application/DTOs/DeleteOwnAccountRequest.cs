namespace InvestindoEmNegocio.Application.DTOs;

public sealed record DeleteOwnAccountRequest(string CurrentPassword, string ConfirmationText);
