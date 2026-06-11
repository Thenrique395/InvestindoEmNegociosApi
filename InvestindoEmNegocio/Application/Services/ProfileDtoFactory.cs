using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class ProfileDtoFactory
{
    internal static UserProfileDto CreateDto(UserProfile profile, User? user)
    {
        var language = string.IsNullOrWhiteSpace(profile.Language) ? "pt-BR" : profile.Language;
        var locales = language
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var currency = profile.Currency ?? "BRL";
        return new UserProfileDto(profile.UserId, profile.FullName, user?.Document ?? string.Empty, profile.Phone, profile.BirthDate,
            user?.AvatarUrl ?? string.Empty, user?.City ?? string.Empty, user?.State ?? string.Empty, user?.Country ?? string.Empty,
            profile.FinancialGoal, profile.CarryOverDay, profile.IntelligenceMode, language, currency, locales);
    }
}
