namespace InvestindoEmNegocio.Application.DTOs;

public record UpdatePreferencesRequest(string Currency, List<string> Locales);
