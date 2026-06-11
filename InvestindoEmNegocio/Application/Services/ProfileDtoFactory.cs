using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class ProfileDtoFactory
{
    internal static UserProfileDto CreateDto(UserProfile profile, string document)
    {
        var language = string.IsNullOrWhiteSpace(profile.Language) ? "pt-BR" : profile.Language;
        var locales = language
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var currency = profile.Currency ?? "BRL";
        return new UserProfileDto(profile.UserId, profile.FullName, document, profile.Phone, profile.BirthDate,
            profile.AvatarUrl, profile.City, profile.State, profile.Country, profile.FinancialGoal, profile.CarryOverDay, profile.IntelligenceMode, language, currency, locales);
    }
}
